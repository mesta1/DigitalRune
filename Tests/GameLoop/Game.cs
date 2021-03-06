using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using DigitalRune.Game.Timing;


namespace Game
{
  /// <summary>
  /// Implements the game loop.
  /// </summary>
  /// <remarks>
  /// The game loop is hooked to <see cref="System.Windows.Forms.Application.Idle"/> event.
  /// (This method is recommended in Tom Miller's blog.)
  /// </remarks>
  class Game
  {
    #region ----- Win32 Helper -----

    [StructLayout(LayoutKind.Sequential)]
    public struct MSG
    {
      public IntPtr hWnd;
      public uint message;
      public IntPtr wParam;
      public IntPtr lParam;
      public uint time;
      public Point pt;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool PeekMessage(out MSG message, IntPtr hWnd, uint messageFilterMin, uint messageFilterMax, uint flags);
    
    public static bool IsApplicationIdle
    {
      get
      {
        MSG message;
        return !PeekMessage(out message, IntPtr.Zero, 0, 0, 0);
      }
    }
    #endregion
    

    private static Window _window;
    private static HighPrecisionClock _clock;
    private static FixedStepTimer _timer;


    /// <summary>
    /// The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
      Application.EnableVisualStyles();
      Application.SetCompatibleTextRenderingDefault(false);

      // Main window
      _window = new Window();

      // Initialization routines
      Initialize();

      // Register game loop
      Application.Idle += GameLoop;

      // Show window, process messages
      Application.Run(_window);
    }


    /// <summary>
    /// Initializes the game.
    /// </summary>
    private static void Initialize()
    {
      _clock = new HighPrecisionClock();
      _timer = new FixedStepTimer(_clock);
      _clock.Start();
      _timer.Start();
    }


    /// <summary>
    /// The main loop ("game loop").
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="eventArgs">
    /// The <see cref="EventArgs"/> instance containing the event data.
    /// </param>
    private static void GameLoop(object sender, EventArgs eventArgs)
    {
      while (IsApplicationIdle /* && game is running */)
      {
        _clock.Update();
        if (_timer.IsRunning)
        {
          if (_timer.IdleTime > TimeSpan.Zero)
          {
            Idle(_timer.IdleTime);
          }
          else if (_timer.DeltaTime > TimeSpan.Zero)
          {
            Update(_timer.DeltaTime);
          }
        }
      }
    }


    /// <summary>
    /// Performs a time step.
    /// </summary>
    /// <param name="deltaTime">The size of the time step.</param>
    private static void Update(TimeSpan deltaTime)
    {
      Console.Out.WriteLine("Update(" + deltaTime + ")");
    }


    /// <summary>
    /// Perform tasks while game is idle.
    /// </summary>
    /// <param name="idleTime">The idle time.</param>
    private static void Idle(TimeSpan idleTime)
    {
      Console.Out.WriteLine("Idle(" + idleTime + ")");
    }
  }
}
