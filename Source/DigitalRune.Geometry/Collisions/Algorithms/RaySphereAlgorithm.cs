// DigitalRune Engine - Copyright (C) DigitalRune GmbH
// This file is subject to the terms and conditions defined in
// file 'LICENSE.TXT', which is part of this source code package.

using System;
using System.Diagnostics;
using DigitalRune.Geometry.Shapes;
using DigitalRune.Mathematics;
using DigitalRune.Mathematics.Algebra;


namespace DigitalRune.Geometry.Collisions.Algorithms
{
  /// <summary>
  /// Computes contact or closest-point information for <see cref="RayShape"/> vs. 
  /// <see cref="SphereShape"/>.
  /// </summary>
  /// <remarks>
  /// This algorithm will fail if it is called for collision objects with other shapes.
  /// </remarks>
  public class RaySphereAlgorithm : CollisionAlgorithm
  {
    // Non-uniformly scaled spheres are not handled by this algorithm. We use 
    // a ray-convex algorithm as fallback.
    private CollisionAlgorithm _fallbackAlgorithm;


    /// <summary>
    /// Initializes a new instance of the <see cref="RaySphereAlgorithm"/> class.
    /// </summary>
    /// <param name="collisionDetection">The collision detection service.</param>
    public RaySphereAlgorithm(CollisionDetection collisionDetection)
      : base(collisionDetection)
    {
    }


    /// <inheritdoc/>
    /// <exception cref="ArgumentException">
    /// <paramref name="contactSet"/> does not contain a <see cref="RayShape"/> and a 
    /// <see cref="SphereShape"/>.
    /// </exception>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1062:Validate arguments of public methods")]
    public override void ComputeCollision(ContactSet contactSet, CollisionQueryType type)
    {
      // Ray vs. sphere has at max 1 contact.
      Debug.Assert(contactSet.Count <= 1);

      // Object A should be the ray.
      // Object B should be the sphere.
      IGeometricObject rayObject = contactSet.ObjectA.GeometricObject;
      IGeometricObject sphereObject = contactSet.ObjectB.GeometricObject;

      // Swap if necessary.
      bool swapped = (sphereObject.Shape is RayShape);
      if (swapped)
        MathHelper.Swap(ref rayObject, ref sphereObject);

      RayShape rayShape = rayObject.Shape as RayShape;
      SphereShape sphereShape = sphereObject.Shape as SphereShape;

      // Check if shapes are correct.
      if (rayShape == null || sphereShape == null)
        throw new ArgumentException("The contact set must contain a ray and a sphere.", "contactSet");

      // Get transformations.
      Vector3F rayScale = rayObject.Scale;
      Vector3F sphereScale = Vector3F.Absolute(sphereObject.Scale);
      Pose rayPose = rayObject.Pose;
      Pose spherePose = sphereObject.Pose;

      // Call other algorithm for non-uniformly scaled spheres.
      if (sphereScale.X != sphereScale.Y || sphereScale.Y != sphereScale.Z)
      {
        if (_fallbackAlgorithm == null)
          _fallbackAlgorithm = CollisionDetection.AlgorithmMatrix[typeof(RayShape), typeof(ConvexShape)];

        _fallbackAlgorithm.ComputeCollision(contactSet, type);
        return;
      }

      // Scale ray and transform ray to world space.
      Ray rayWorld = new Ray(rayShape);
      rayWorld.Scale(ref rayScale);
      rayWorld.ToWorld(ref rayPose);

      // Scale sphere.
      float sphereRadius = sphereShape.Radius * sphereScale.X;
      float sphereRadiusSquared = sphereRadius * sphereRadius;

      // Call line segment vs. sphere for closest points queries.
      if (type == CollisionQueryType.ClosestPoints || type == CollisionQueryType.Boolean)
      {
        // ----- Find point on ray closest to the sphere.
        // Make a line segment vs. point (sphere center) check.
        Debug.Assert(rayWorld.Direction.IsNumericallyNormalized, "The ray direction should be normalized.");
        LineSegment segment = new LineSegment(rayWorld.Origin, rayWorld.Origin + rayWorld.Direction * rayWorld.Length);

        Vector3F segmentPoint;
        Vector3F sphereCenter = spherePose.Position;
        GeometryHelper.GetClosestPoints(segment, sphereCenter, out segmentPoint);

        Vector3F normal = sphereCenter - segmentPoint;
        float distanceSquared = normal.LengthSquared;
        float penetrationDepthSquared = sphereRadiusSquared - distanceSquared;
        contactSet.HaveContact = penetrationDepthSquared >= 0;

        if (type == CollisionQueryType.Boolean)
          return;

        if (penetrationDepthSquared <= 0)
        {
          // Separated or touching objects.
          Vector3F position;
          if (normal.TryNormalize())
          {
            Vector3F spherePoint = sphereCenter - normal * sphereRadius;
            position = (spherePoint + segmentPoint) / 2;
          }
          else
          {
            position = segmentPoint;
            normal = Vector3F.UnitY;
          }

          if (swapped)
            normal = -normal;

          float penetrationDepth = -((float)Math.Sqrt(distanceSquared) - sphereRadius);

          // Update contact set.
          Contact contact = ContactHelper.CreateContact(contactSet, position, normal, penetrationDepth, contactSet.HaveContact);
          ContactHelper.Merge(contactSet, contact, type, CollisionDetection.ContactPositionTolerance);
          return;
        }

        // Otherwise, we have a penetrating contact.
        // Compute 1 contact ...
      }

      // First assume no contact.
      contactSet.HaveContact = false;

      // See SOLID and Bergen: "Collision Detection in Interactive 3D Environments", pp. 70.
      // Compute in sphere local space.

      // Transform ray to local space of sphere.
      Ray ray = rayWorld;
      ray.ToLocal(ref spherePose);

      Vector3F s = ray.Origin;      // ray source
      Vector3F d = ray.Direction;   // ray direction

      Vector3F r = d * ray.Length;                        // ray
      float rayLengthSquared = ray.Length * ray.Length;   // ||r||²
      float δ = -Vector3F.Dot(s, r);                      // -s∙r
      float σ = δ * δ - rayLengthSquared * (s.LengthSquared - sphereRadiusSquared);
      if (σ >= 0)
      {
        // The infinite ray intersects.
        float sqrtσ = (float)Math.Sqrt(σ);

        float λ2 = (δ + sqrtσ) /* / rayLengthSquared */; // Division can be ignored. Only sign is relevant.
        if (λ2 >= 0)
        {
          // Ray shoots to sphere.
          float λ1 = (δ - sqrtσ) / rayLengthSquared;

          if (λ1 <= 1)
          {
            // Ray hits sphere.
            contactSet.HaveContact = true;

            Debug.Assert(type != CollisionQueryType.Boolean); // Was handled before.

            float penetrationDepth;
            Vector3F normal;
            if (λ1 > 0)
            {
              // λ1 shows the entry point. λ2 shows the exit point.
              penetrationDepth = λ1 * ray.Length;   // Distance from ray origin to entry point (hit).
              normal = -(s + r * λ1);               // Entry point (hit).
            }
            else
            {
              // Ray origin is in the sphere.
              penetrationDepth = 0;
              normal = -s;
            }

            Vector3F position = rayWorld.Origin + rayWorld.Direction * penetrationDepth;

            normal = spherePose.ToWorldDirection(normal);

            if (!normal.TryNormalize())
              normal = Vector3F.UnitY;

            if (swapped)
              normal = -normal;

            // Update contact set.
            Contact contact = ContactHelper.CreateContact(contactSet, position, normal, penetrationDepth, true);
            ContactHelper.Merge(contactSet, contact, type, CollisionDetection.ContactPositionTolerance);
          }
        }
      }
    }
  }
}
