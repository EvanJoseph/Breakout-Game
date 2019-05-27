using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Gaming.Input;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

// Known Bugs:
    // Stuck in wall (fix attempted)
namespace Breakout
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private bool isLeftKeyDown = false;
        private bool isRightKeyDown = false;
        private bool isPaused = false;
        private DispatcherTimer timer;
        private List<BrickRow> brickrows;
        private Random rng;
        private int rowspacing = 20;
        private int brickwidth = 75;
        private int gameSpeed = 7;
        private Gamepad gamepad;
        
        // List of balls, main ball is always index 0;
        private List<Ellipse> Balls;
        private List<double[]> BallVelocityUV; // { X, Y } ; remember +X is right, +Y is down; 0, 0 is at top left of page "UV" stands for unit vector
        private enum BallType { Main, IncreaseWidth, DecreaseWidth, ExtraLife, ExtraBall, RemoveExtraBall };
        private List<BallType> BallKinds;
        private bool hard_mode = false;
        private int Nlives = 1;
        private int probMult = 2;
        private int score = 0;

        public MainPage()
        {
            // disable the TV safe display zone to get images to the very edge
            Windows.UI.ViewManagement.ApplicationView.GetForCurrentView().SetDesiredBoundsMode(Windows.UI.ViewManagement.ApplicationViewBoundsMode.UseCoreWindow);

            this.InitializeComponent();

            // set a timer to work with GameHandler
            timer = new DispatcherTimer();
            timer.Tick += GameHandler;
            timer.Interval = new TimeSpan(0, 0, 0, 0, 2);
            timer.Start();

            brickrows = new List<BrickRow>();
            rng = new Random();
            Balls = new List<Ellipse>() { Ball };
            BallVelocityUV = new List<double[]> { new double[] { 0, 1 } };
            BallKinds = new List<BallType> { 0 };

            // add initial set of brick rows:
            for (int ii = 0; ii < 6; ii++)
            {
                if (ii % 3== 0)
                {
                    AddBrickRow(empty:true);
                }
                else
                {
                    AddBrickRow();
                }
            }

            // create key handler events
            Window.Current.CoreWindow.KeyDown += KeyPressHandler;
            Window.Current.CoreWindow.KeyUp += KeyReleaseHandler;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            StartPage.MainArgs m = (StartPage.MainArgs)e.Parameter;
            hard_mode = m.hard_mode_on;
            probMult = m.item_prob_mult;
        }

        private void GameHandler(object sender, object e)
        {
            // move ball and paddle, keeping paddle in bounds, change speed, or quit game
            MoveBall();
            MovePaddle();
            ChangeGameSpeed();
            QuitGame();
            KeepPaddleInBounds();

            // check if the ball collided with something
            DetectBrickCollision();
            DetectBoundaryCollision();
        }

        private void MoveBall()
        {
            for (int ii = 0; ii < Balls.Count; ii++)
            {
                MoveBall_helper(ii);
            }
        }

        // Separated from MoveBall, to allow other functions to move a specific ball.
        // This is an attempt to fix the 'stuck in wall' bug.
        private void MoveBall_helper(int ii)
        {
            double[] pos = new double[2] { Balls[ii].Margin.Left, Balls[ii].Margin.Top }; // { X, Y }

            // Don't allow ball to have zero Y velocity.
            if (BallVelocityUV[ii][1] < 0.1 && BallVelocityUV[ii][1] >= 0)
            {
                BallVelocityUV[ii][1] = 0.1;
            }
            else if (BallVelocityUV[ii][1] > -0.1 && BallVelocityUV[ii][1] < 0)
            {
                BallVelocityUV[ii][1] = -0.1;
            }
            BallVelocityUV[ii] = MakeUnitVector(BallVelocityUV[ii]);

            int speed = (!(BallKinds[ii] == BallType.Main || BallKinds[ii] == BallType.ExtraBall)) ? gameSpeed / 2 : gameSpeed; // set power ups to move a third as fast as main ball.
            pos[0] += BallVelocityUV[ii][0] * speed;
            pos[1] += BallVelocityUV[ii][1] * speed;
            Balls[ii].Margin = new Thickness(pos[0], pos[1], 0, 0);
        }

        private void MovePaddle()
        {
            int mult = 3;
            // move paddle when the left stick or d-pad is used
            if (Gamepad.Gamepads.Count > 0)
            {
                gamepad = Gamepad.Gamepads.First();
                var reading = gamepad.GetCurrentReading();
                if (reading.LeftThumbstickX < -0.1 || reading.Buttons.HasFlag(GamepadButtons.DPadLeft))
                {
                    Paddle.Margin = new Thickness(Paddle.Margin.Left - mult * gameSpeed, Paddle.Margin.Top, 0, 0);
                }
                if (reading.LeftThumbstickX > 0.1 || reading.Buttons.HasFlag(GamepadButtons.DPadRight))
                {
                    Paddle.Margin = new Thickness(Paddle.Margin.Left + mult * gameSpeed, Paddle.Margin.Top, 0, 0);
                }
            }

            //move paddle when the left or right arrow key is used
            if (isLeftKeyDown)
            {
                Paddle.Margin = new Thickness(Paddle.Margin.Left - mult * gameSpeed, Paddle.Margin.Top, 0, 0);
            }
            if (isRightKeyDown)
            {
                Paddle.Margin = new Thickness(Paddle.Margin.Left + mult * gameSpeed, Paddle.Margin.Top, 0, 0);
            }
        }

        private void KeepPaddleInBounds()
        {
            // keep the paddle at the left or right edge if it tries to move out of bounds
            if (Paddle.Margin.Left < LeftWall.Margin.Left)
            {
                Paddle.Margin = new Thickness(LeftWall.Margin.Left, Paddle.Margin.Top, 0, 0);
            }
            else if (Paddle.Margin.Left + Paddle.Width > RightWall.Margin.Left)
            {
                Paddle.Margin = new Thickness(RightWall.Margin.Left - Paddle.Width, Paddle.Margin.Top, 0, 0);
            }
        }

        private void ChangeGameSpeed()
        {
            // change the ball speed if the right stick is used
            if (Gamepad.Gamepads.Count > 0)
            {
                gamepad = Gamepad.Gamepads.First();
                var reading = gamepad.GetCurrentReading();
                if (reading.RightThumbstickY < -0.1)
                {
                    // prevent speed from reaching zero
                    if (gameSpeed > 1)
                    {
                        gameSpeed -= 1;
                    }
                }
                if (reading.RightThumbstickY > 0.1)
                {
                    gameSpeed += 1;
                }
            }
        }

        private void QuitGame()
        {
            // quit the game if the back button is pressed
            if (Gamepad.Gamepads.Count > 0)
            {
                gamepad = Gamepad.Gamepads.First();
                var reading = gamepad.GetCurrentReading();
                if (reading.Buttons.HasFlag(GamepadButtons.View))
                {
                    timer.Stop();
                    this.Frame.Navigate(typeof(StartPage), "");
                }
            }
        }

        private void PauseGame()
        {
            // pause the game if the start button is pressed
            if (Gamepad.Gamepads.Count > 0)
            {
                gamepad = Gamepad.Gamepads.First();
                var reading = gamepad.GetCurrentReading();
                if (reading.Buttons.HasFlag(GamepadButtons.Menu))
                {
                    if (!isPaused)
                    {
                        PauseText.Visibility = Visibility.Visible;
                        timer.Tick -= GameHandler;
                    }
                    else
                    {
                        PauseText.Visibility = Visibility.Collapsed;
                        timer.Tick += GameHandler;
                    }
                    isPaused = !isPaused;
                }
            }
        }

        private void DetectBoundaryCollision()
        {
            for (int ii = 0; ii < Balls.Count; ii++)
            {
                // check if the ball has hit a wall
                if (Balls[ii].Margin.Left == LeftWall.Margin.Left + LeftWall.Width || Balls[ii].Margin.Left + gameSpeed < LeftWall.Margin.Left + LeftWall.Width)
                {
                    BallVelocityUV[ii][0] *= -1; // reverse X velocity component of main ball
                    MoveBall_helper(ii); // attempt to fix stuck in wall bug.
                }
                if (Balls[ii].Margin.Left + Balls[ii].Width == RightWall.Margin.Left || Balls[ii].Margin.Left + Balls[ii].Width + gameSpeed > RightWall.Margin.Left)
                {
                    BallVelocityUV[ii][0] *= -1;
                    MoveBall_helper(ii);
                }
                if (Balls[ii].Margin.Top == Ceiling.Margin.Top - Ceiling.Height || Balls[ii].Margin.Top - gameSpeed < Ceiling.Margin.Top - Ceiling.Height)
                {
                    BallVelocityUV[ii][1] *= -1;
                    MoveBall_helper(ii);
                }

                // check if something has hit the paddle
                if (Balls[ii].Margin.Top + Balls[ii].Height + gameSpeed >= Paddle.Margin.Top && Balls[ii].Margin.Top + Balls[ii].Height <= Paddle.Margin.Top
                    && Balls[ii].Margin.Left >= Paddle.Margin.Left && Balls[ii].Margin.Left + Balls[ii].Width <= Paddle.Margin.Left + Paddle.Width)
                {
                    // check if it was a ball
                    if (BallKinds[ii] == BallType.Main || BallKinds[ii] == BallType.ExtraBall)
                    {
                        BallVelocityUV[ii] = PaddleCollision(BallVelocityUV[ii]);
                    }
                    else // check if it was a power up
                    {
                        ii -= DoPowerUp(ii);
                        // Remove Power up from game: now in DoPowerUp
                        continue;
                    }
                }

                // check if the ball has hit the bottom of the screen
                if (Balls[ii].Margin.Top > LeftWall.Margin.Top + LeftWall.Height || Balls[ii].Margin.Top + Balls[ii].Height < LeftWall.Margin.Top)
                {
                    if (BallKinds[ii] == BallType.Main)
                    {
                        LoseLife();
                        BallVelocityUV[ii][1] *= -1;
                        MoveBall_helper(ii);
                    }
                    else if (BallKinds[ii] == BallType.ExtraBall)
                    {
                        if (hard_mode)
                        {
                            LoseLife();
                        }
                        removeBall(ii);
                        ii--;
                        continue;
                    }
                    else
                    {
                        // Remove Power up from game.
                        removeBall(ii);
                        ii--;
                        continue;
                    }
                }
            }
        }

        private void LoseLife()
        {
            //if we have an extra life, we don't lose yet. the ball will be reflected and we will lose our extra life
            if (Nlives > 0)
            {
                Nlives--;
                NumLivesText.Text = String.Format("# Lives = {0}.", Nlives);
            }
            else
            {
                timer.Stop();
                this.Frame.Navigate(typeof(StartPage), String.Format("Game Over. Score = {0}", score));
            }
        }


        /// <summary>
        /// Modifies ball velocity according to contact location on the paddle.
        /// </summary>
        private double[] PaddleCollision(double[] newvel)
        {
            double dist_from_mid = (Balls[0].Margin.Left + Balls[0].Width / 2) - (Paddle.Margin.Left + Paddle.Width/2); // middle of ball - middle of paddle
            double[] velmod = { dist_from_mid, -Paddle.Height*2 };
            velmod = MakeUnitVector(velmod);
            
            if (hard_mode)
            {
                newvel[0] += velmod[0];
                newvel[1] += velmod[1];
                newvel = MakeUnitVector(newvel);
            }
            else
            {
                newvel = velmod;
            }

            return newvel;
        }

        /// <summary>
        /// Makes [x,y] pair into unit vector with magnitude = 1
        /// </summary>
        /// <param name="vel"></param>
        /// <returns></returns>
        private double[] MakeUnitVector(double[] vel)
        {
            double mag = Math.Sqrt(Math.Pow(vel[0], 2) + Math.Pow(vel[1], 2));
            vel[0] /= mag;
            vel[1] /= mag;
            return vel;
        }

        private void DetectBrickCollision()
        {
            for (int ii = 0; ii < Balls.Count; ii++)
            {
                if ( !(BallKinds[ii] == BallType.Main || BallKinds[ii] == BallType.ExtraBall) )
                {
                    continue;
                }
                // check for collision with a brick
                Rectangle top = BrickCollision_helper((int)(Balls[ii].Margin.Left + Balls[ii].Width / 2), (int)Balls[ii].Margin.Top); // on ball's top side
                if (top != null)
                {
                    BallVelocityUV[ii][1] *= -1; // invert Y velocity
                    dropPowerUp();
                    GameGrid.Children.Remove(top);
                    UpdateScore();
                }
                Rectangle bottom = BrickCollision_helper((int)(Balls[ii].Margin.Left + Balls[ii].Width / 2), (int)(Balls[ii].Margin.Top + Balls[ii].Height)); // on ball's bottom side
                if (bottom != null)
                {
                    BallVelocityUV[ii][1] *= -1;
                    dropPowerUp();
                    GameGrid.Children.Remove(bottom);
                    UpdateScore();
                }
                Rectangle left = BrickCollision_helper((int)(Balls[ii].Margin.Left), (int)(Balls[ii].Margin.Top + Balls[ii].Height / 2)); // on ball's left side
                if (left != null)
                {
                    BallVelocityUV[ii][0] *= -1;
                    dropPowerUp();
                    GameGrid.Children.Remove(left);
                    UpdateScore();
                }
                Rectangle right = BrickCollision_helper((int)(Balls[ii].Margin.Left + Balls[ii].Width), (int)(Balls[ii].Margin.Top + Balls[ii].Height / 2)); // on ball's right side
                if (right != null)
                {
                    BallVelocityUV[ii][0] *= -1;
                    dropPowerUp();
                    GameGrid.Children.Remove(right);
                    UpdateScore();
                }
            }
        }

        private Rectangle BrickCollision_helper(int hpos, int vpos)
        {
            Rectangle b = null;

            // convert ball edge position to brick-grid indices:
            int rowindex = (vpos - (int)Ceiling.Margin.Top) / rowspacing;
            int colindex = (hpos - (int)LeftWall.Margin.Left) / brickwidth;

            // correct for any out of index errors
            if (rowindex >= brickrows.Count ||
                rowindex < 0 ||
                colindex >= brickrows[rowindex].bricks.Count ||
                colindex < 0)
            {
                return null;
            }
            
            b = brickrows[rowindex].bricks[colindex];

            if (b != null)
            { // remove brick from brickrows now, while we conveniently know its indices.
                brickrows[rowindex].bricks[colindex] = null; // using null, so that we can keep the spot empty, in order to keep index positions consistent.
            }
            return b;
        }

        private void UpdateScore(int inc=1)
        {
            score += inc;
            ScoreText.Text = String.Format("Score = {0}.", score);
        }


        private void KeyPressHandler(Windows.UI.Core.CoreWindow sender, Windows.UI.Core.KeyEventArgs e)
        {
            // check if the pause button on the controller has been pressed.
            PauseGame();

            // check if an input key has been pressed
            if (e.VirtualKey == Windows.System.VirtualKey.Left)
            {
                isLeftKeyDown = true;
            }
            else if (e.VirtualKey == Windows.System.VirtualKey.Right)
            {
                isRightKeyDown = true;
            }
            else if (e.VirtualKey == Windows.System.VirtualKey.PageUp)
            {
                gameSpeed += 1;
            }
            else if (e.VirtualKey == Windows.System.VirtualKey.PageDown)
            {
                // prevent speed from reaching zero
                if (gameSpeed > 1)
                {
                    gameSpeed -= 1;
                }
            }
            else if (e.VirtualKey == Windows.System.VirtualKey.Escape)
            {
                timer.Stop();
                this.Frame.Navigate(typeof(StartPage), "");
            }
            else if (e.VirtualKey == Windows.System.VirtualKey.Space)
            {
                if (!isPaused)
                {
                    PauseText.Visibility = Visibility.Visible;
                    timer.Tick -= GameHandler;
                }
                else
                {
                    PauseText.Visibility = Visibility.Collapsed;
                    timer.Tick += GameHandler;
                }
                isPaused = !isPaused;
            }
        }

        private void KeyReleaseHandler(Windows.UI.Core.CoreWindow sender, Windows.UI.Core.KeyEventArgs e)
        {
            //check if an input key has been released
            if (e.VirtualKey == Windows.System.VirtualKey.Left)
            {
                isLeftKeyDown = false;
            }
            else if (e.VirtualKey == Windows.System.VirtualKey.Right)
            {
                isRightKeyDown = false;
            }
        }

        private void AddBrickRow(bool empty=false, int ind = -1)
        {
            bool insert = ind < 0 ? false : true;
            if (ind < 0)
            {
                ind = brickrows.Count;
            }
            // create a row of bricks
            BrickRow br = new BrickRow()
            {
                vpos = ind * rowspacing + (int)Ceiling.Margin.Top,
                bricks = new List<Rectangle>()
            };
            int hpos = (int)LeftWall.Margin.Left;
            while (hpos < (int)RightWall.Margin.Left)
            {
                if (empty)
                {
                    br.bricks.Add(null);
                }
                else
                {
                    AddBrick(br, hpos);
                }
                hpos = hpos + brickwidth;
            }

            if (insert)
            {
                InsertBrickRow(ind, br);
            }
            else
            {
                brickrows.Add(br);
            }
            
        }

        // Currently unused: used for timed add brick row event.
        private void InsertBrickRow(int ind, BrickRow br)
        {
            brickrows.Insert(ind, br);
            // reload all bricks into GameGrid; ( maybe a better way to insert, but this makes sure all bricks are in the right spot.
            for (int ii = 0; ii < brickrows.Count; ii++)
            {
                int vpos = ii * rowspacing + (int)Ceiling.Margin.Top;
                var brick = brickrows[ii];
                    brick.vpos = vpos; 
                for (int jj = 0; jj < brickrows[ii].bricks.Count; jj++)
                {
                    if (brickrows[ii].bricks[jj] != null)
                    {
                        GameGrid.Children.Remove(brickrows[ii].bricks[jj]);
                        double hpos = brickrows[ii].bricks[jj].Margin.Left;
                        brickrows[ii].bricks[jj].Margin = new Thickness(hpos, vpos, 0, 0);
                        GameGrid.Children.Add(brickrows[ii].bricks[jj]);
                    }
                }
            }
        }

        private void AddBrick(BrickRow br, int hpos)
        {
            // create a single brick

            var b = new Rectangle()
            {
                Name = "Brick",
                Margin = new Thickness(hpos, br.vpos, 0, 0),
                Height = 15,
                Width = brickwidth,
                Stroke = new SolidColorBrush(Color.FromArgb(255, 0, 0, 0)),
                Fill = GetColor(),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };
            br.bricks.Add(b);
            GameGrid.Children.Add(b);
        }

        private bool IsEmptyBrickRow(int ind)
        {
            bool isempty = true;
            for (int ii = 0; ii < brickrows[ind].bricks.Count; ii++)
            {
                if (brickrows[ind].bricks[ii] != null)
                {
                    isempty = false;
                    break;
                }
            }
            return isempty;
        }

        private struct BrickRow
        {
            public int vpos;
            public List<Rectangle> bricks;
        }

        //Fixed up the code a bit to make it happen less often. It had been doubling the paddle and adding an ellipse for every brick I hit.
        //The numbers can be tweaked if you want something to happen more often. I feel like a small increase/decrease in paddle
        //size is better than doubling, because doubling/halving can possibly get out of hand very quickly.
        //I was thinking of adding an extra life power up, maybe set some hasExtraLife bool variable to true and check for that when determining
        //if the game is over by crossing the line, just a suggestion.
        private void dropPowerUp()
        {
            int n;

            // NOTE: to enable mutual exclusion, place a return statement at the end of each probability if block.
            //       Also, note that with mutual exclusion in this way, later items will be less probable that eariler items in the list, even with the same probability.
            n = rng.Next(100);
            if (n < 5 * probMult) // is 5% chance at probMult = 1
            {
                addBall(BallType.IncreaseWidth);
                //return;
            }
            n = rng.Next(100);
            if (n < 7 * probMult)
            {
                addBall(BallType.DecreaseWidth);
                //return;
            }
            n = rng.Next(100);
            if (n < 3 * probMult)
            {
                addBall(BallType.ExtraLife);
                //return;
            }
            n = rng.Next(100);
            if (n < 5 * probMult)
            {
                addBall(BallType.ExtraBall);
                //return;
            }
            n = rng.Next(100);
            if (n < 5 * probMult)
            {
                addBall(BallType.RemoveExtraBall);
                //return;
            }
            if (hard_mode)
            {
                // Hard Mode stuff:
            }

        }

        private void addBall(BallType t) // adds a new ball/powerup
        {
            var e = new Ellipse()
            {
                Name = string.Format("PowerUp_{0}", Balls.Count),
                Height = Balls[0].Height,
                Width = Balls[0].Width,
                Margin = Balls[0].Margin,
                Stroke = Balls[0].Stroke
            };


            switch (t)
            {
                case BallType.Main: // is Main ball.
                    e.Fill = GetColor("b"); // blue
                    break;
                case BallType.IncreaseWidth: // IncreaseWidth
                    e.Fill = GetColor("g"); // green
                    break;
                case BallType.DecreaseWidth: // DecreaseWidth
                    e.Fill = GetColor("k"); // black
                    break;
                case BallType.ExtraLife: // ExtraLife
                    e.Fill = GetColor("r"); // red
                    break;
                case BallType.ExtraBall: // ExtraBall
                    e.Fill = GetColor("c"); // cyan
                    break;
                case BallType.RemoveExtraBall: // RemoveExtraBall
                    e.Fill = GetColor("-c"); // transparent cyan
                    break;
            }

            Balls.Add(e);
            BallVelocityUV.Add(new double[] { 0, 1 }); // initialize to going straight down.
            BallKinds.Add(t);
            GameGrid.Children.Add(e);
            e.HorizontalAlignment = Balls[0].HorizontalAlignment;
            e.VerticalAlignment = Balls[0].VerticalAlignment;
            MoveBall_helper(Balls.IndexOf(e)); // move ball just spawned, attempt to fix red balls shorten bug.
        }


        private void removeBall(Ellipse b)
        {
            int ii = Balls.IndexOf(b);
            GameGrid.Children.Remove(b);
            Balls.Remove(b);
            BallVelocityUV.RemoveAt(ii);
            BallKinds.RemoveAt(ii);
        }
        private void removeBall(int ii)
        {
            GameGrid.Children.Remove(Balls[ii]);
            Balls.RemoveAt(ii);
            BallVelocityUV.RemoveAt(ii);
            BallKinds.RemoveAt(ii);
        }

        private int DoPowerUp(int ii)
        {
            int Nremoved = 1;
            BallType t = BallKinds[ii];
            switch (t)
            {
                // case 0: is Main ball, no powerup.
                case BallType.IncreaseWidth: // IncreaseWidth
                    Paddle.Width = 5 * Paddle.Width / 4; // increase paddle size
                    break;
                case BallType.DecreaseWidth: // DecreaseWidth
                    Paddle.Width = 3 * Paddle.Width / 4; // decrease paddle size
                    break;
                case BallType.ExtraLife: // ExtraLife
                    Nlives++;
                    // display extra life on screen
                    NumLivesText.Text = String.Format("# Lives = {0}.", Nlives);
                    break;
                // case 4: ExtraBall, doesn't have a powerup effect

                case BallType.RemoveExtraBall: // RemoveExtraBall
                    if (hard_mode)
                    {
                        // hard mode doesn't remove extras. if you come up with a better idea for this on hard mode, put it here.
                    }
                    else
                    {
                        for (int jj = Balls.Count - 1; jj > 0; jj--)
                        {
                            if (BallKinds[jj] == BallType.ExtraBall)
                            {
                                if (jj < ii)
                                {  // This, and Nremoved is to fix complications with indexing, when removing two balls
                                    ii--;
                                    Nremoved++;
                                }
                                removeBall(jj);
                                break;
                            }
                        }
                    }
                    break;
            }
            removeBall(ii);
            return Nremoved;
        }

        private SolidColorBrush GetColor(string str="")
        {
            SolidColorBrush c;
            switch (str)
            {
                case "k":
                    c = new SolidColorBrush(Color.FromArgb(255, (byte)(0), (byte)(0), (byte)(0)));
                    break;
                case "b":
                    c = new SolidColorBrush(Color.FromArgb(255, (byte)(0), (byte)(0), (byte)(255)));
                    break;
                case "g":
                    c = new SolidColorBrush(Color.FromArgb(255, (byte)(0), (byte)(255), (byte)(0)));
                    break;
                case "r":
                    c = new SolidColorBrush(Color.FromArgb(255, (byte)(255), (byte)(0), (byte)(0)));
                    break;
                case "c":
                    c = new SolidColorBrush(Color.FromArgb(255, (byte)(0), (byte)(255), (byte)(255)));
                    break;
                case "-c":
                    c = new SolidColorBrush(Color.FromArgb(50, (byte)(0), (byte)(255), (byte)(255)));
                    break;
                default: // random
                    c = new SolidColorBrush(Color.FromArgb(255, (byte)rng.Next(255), (byte)rng.Next(255), (byte)rng.Next(255)));
                    break;
            }
            return c;
        }

    }
}