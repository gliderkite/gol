using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Life;
using Microsoft.Win32;

namespace Life_App
{
    /// <summary>
    /// Main window.
    /// </summary>
    /// <remarks> Life - A Conway's Game of Life Emulator
    /// http://en.wikipedia.org/wiki/Conway%27s_Game_of_Life</remarks>
    public partial class MainWindow : Window
    {


        #region Constants



        /// <summary>
        /// Update speed slow [ms].
        /// </summary>
        private const int TIME_SLOW = 1000;

        /// <summary>
        /// Update speed normal [ms].
        /// </summary>
        private const int TIME_NORMAL = 400;

        /// <summary>
        /// Update speed fast [ms].
        /// </summary>
        private const int TIME_FAST = 100;

        /// <summary>
        /// Update speed very fast [ms].
        /// </summary>
        private const int TIME_VERY_FAST = 1;

        /// <summary>
        /// Ellipses max size.
        /// </summary>
        private const int MAX_ZOOM = 40;

        /// <summary>
        /// Ellipses min size.
        /// </summary>
        private const int MIM_ZOOM = 2;

        /// <summary>
        /// Ellipses normal size.
        /// </summary>
        private const int NORMAL_ZOOM = 10;

        /// <summary>
        /// Ellipses size in edit mode.
        /// </summary>
        private const int EDIT_SIZE = 30;

        /// <summary>
        /// Viewport max size.
        /// </summary>
        private const int MAX_SIZE = 5000;



        #endregion



        #region Fields



        /// <summary>
        /// Game of Life universe.
        /// </summary>
        private readonly Universe universe = new Universe();

        /// <summary>
        /// List of ellipses which represent cells.
        /// </summary>
        private readonly List<Ellipse> cells = new List<Ellipse>();

        /// <summary>
        /// List of ellipses which represent cells in edit mode.
        /// </summary>
        private readonly List<Ellipse> editCells = new List<Ellipse>();

        /// <summary>
        /// Timer used to update the population.
        /// </summary>
        private readonly DispatcherTimer timer = new DispatcherTimer();

        /// <summary>
        /// Object used for mutual exclusion.
        /// </summary>
        private readonly object mutex = new object();

        /// <summary>
        /// Indica se il timer deve rimanere bloccato o meno.
        /// </summary>
        private bool stopTimer = false;

        /// <summary>
        /// Shift used to center the population along the axis of abscissas.
        /// </summary>
        private double shiftX = 0;

        /// <summary>
        /// Shift used to center the population along the axis of ordinates.
        /// </summary>
        private double shiftY = 0;

        /// <summary>
        /// Dimensione corrente da applicare alle ellissi.
        /// </summary>
        private int currentZoom = NORMAL_ZOOM;

        /// <summary>
        /// Cells fill color.
        /// </summary>
        private Brush cellFill = new SolidColorBrush(Colors.LightGreen);

        /// <summary>
        /// Cells stroke color.
        /// </summary>
        private Brush cellStroke = new SolidColorBrush(Colors.Black);

        /// <summary>
        /// Indicates whether it is possible to move scroll bars programmatically.
        /// </summary>
        private bool initScrollBar = false;

        /// <summary>
        /// Indicates whether you are in edit mode.
        /// </summary>
        private bool editMode = false;

        /// <summary>
        /// Indicates whether the update has been suspoended.
        /// </summary>
        private bool paused = false;



        #endregion



        /// <summary>
        /// Default constructor.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
        }


        /// <summary>
        /// Canvas loaded event.
        /// </summary>
        private void Viewport_Loaded(object sender, RoutedEventArgs e)
        {
            // Init the timer
            Debug.Assert(timer != null);
            timer.Tick += timer_Tick;
            timer.Interval = TimeSpan.FromMilliseconds(TIME_NORMAL);

            Debug.Assert(cellFill != null && cellStroke != null);

            // Freeze cells fill and stroke
            if (cellFill.CanFreeze)
                cellFill.Freeze();
            if (cellStroke.CanFreeze)
                cellStroke.Freeze();
        }


        /// <summary>
        /// Main windowd size changed event.
        /// </summary>
        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            lock (mutex)
            {
                // returns if we are not in edit mode
                if (!editMode)
                    return;
            }

            // If we are in edit mode we have to redraw the grid
            DrawGrid();
        }


        /// <summary>
        /// Scroll changed event.
        /// </summary>
        private void SViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            lock (mutex)
            {
                // returns if we can't change scrollbars positions
                if (!initScrollBar)
                    return;
            }

            // move to the center of the viewport
            SViewer.ScrollToVerticalOffset(SViewer.ScrollableHeight / 2);
            SViewer.ScrollToHorizontalOffset(SViewer.ScrollableWidth / 2);

            lock (mutex)
                initScrollBar = false;
        }


        /// <summary>
        /// Time tick event: display current population and update for the next generation.
        /// </summary>
        private void timer_Tick(object sender, EventArgs e)
        {
            // stops the timer: we can update the population before another call is made
            timer.Stop();

            // Dysplay status information
            TBlockGeneraton.Text = String.Format("Population: {0} - Generation: {1}", universe.Count, universe.Generation);

            // Check if the population die out
            if (universe.Count == 0)
            {
                // clear the viewport
                Viewport.Children.Clear();

                // disable menu items
                MItemPauseResume.IsEnabled = false;
                MItemSpeed.IsEnabled = false;
                MItemZoomMinus.IsEnabled = false;
                MItemZoomPlus.IsEnabled = false; 
                MItemSave.IsEnabled = false;

                // display the "Game Over" message box
                MessageBox.Show(String.Format("The population die out after {0} generations!", universe.Generation), 
                    "Game Over", MessageBoxButton.OK, MessageBoxImage.Asterisk);
            }
            else
            {
                // enable save mode
                MItemSave.IsEnabled = true;

                // get the population
                var population = universe.Population;
                int prevCount = cells.Count;

                // if we need more ellipses create others
                for (int i = 0; i < population.Count - prevCount; i++)
                    cells.Add(GetCellShape(currentZoom));

                // assert we have enough ellipses
                Debug.Assert(cells.Count >= population.Count);

                // Set cells position
                for (int i = 0; i < population.Count; i++)
                {
                    double left = Math.Abs(population[i].X) * currentZoom + shiftX;
                    double top = Math.Abs(population[i].Y) * currentZoom + shiftY;

                    //if (left > Viewport.ActualWidth || top > Viewport.ActualHeight)
                    //    cells[i].Visibility = Visibility.Hidden;

                    Canvas.SetLeft(cells[i], left);
                    Canvas.SetTop(cells[i], top);
                }

                // add cells to the viewport if needed
                for (int i = Viewport.Children.Count; i < population.Count; i++)
                    Viewport.Children.Add(cells[i]);

                // remove cells from viewport if needed
                if (population.Count < Viewport.Children.Count)
                    Viewport.Children.RemoveRange(population.Count, Viewport.Children.Count - population.Count);

                // update the population for the next generation
                universe.UpdatePopulation();

                // restart the timer
                timer.Start();
            }
        }


        /// <summary>
        /// Gets the graphic that represents the single cell.
        /// </summary>
        /// <returns></returns>
        private Ellipse GetCellShape(double size)
        {
            return new Ellipse
            {
                Stroke = cellStroke,
                Fill = cellFill,
                Width = size,
                Height = size,
            };
        }


        /// <summary>
        /// Increases zoom properly.
        /// </summary>
        private void IncreaseZoom()
        {
            if (currentZoom >= MAX_ZOOM)
                currentZoom = MAX_ZOOM;
            else if (currentZoom < NORMAL_ZOOM)
                currentZoom += 2;
            else
                currentZoom += 4;

            lock (mutex)
            {
                // returns if the update is running
                if (!paused)
                    return;
            }

            var population = universe.Population;

            // Set the new cells position
            for (int i = 0; i < population.Count; i++)
            {
                double left = Math.Abs(population[i].X) * currentZoom + shiftX;
                double top = Math.Abs(population[i].Y) * currentZoom + shiftY;

                Canvas.SetLeft(cells[i], left);
                Canvas.SetTop(cells[i], top);
            }
        }


        /// <summary>
        /// Decreases zoom properly.
        /// </summary>
        private void DecreaseZoom()
        {
            if (currentZoom <= MIM_ZOOM)
                currentZoom = MIM_ZOOM;
            else if (currentZoom < NORMAL_ZOOM)
                currentZoom -= 2;
            else
                currentZoom -= 4;

            lock (mutex)
            {
                // returns if the update is running
                if (!paused)
                    return;
            }

            var population = universe.Population;

            // Set the new cells position
            for (int i = 0; i < population.Count; i++)
            {
                double left = Math.Abs(population[i].X) * currentZoom + shiftX;
                double top = Math.Abs(population[i].Y) * currentZoom + shiftY;

                Canvas.SetLeft(cells[i], left);
                Canvas.SetTop(cells[i], top);
            }
        }


        /// <summary>
        /// Draw a simple grid of lines on the viewport.
        /// </summary>
        private void DrawGrid()
        {
            // clear the viewport
            Viewport.Children.Clear();

            // Set width and height properly
            Viewport.Width = MainPanel.ActualWidth - SystemParameters.ScrollWidth;
            Viewport.Height = MainPanel.ActualHeight - Menu.ActualHeight - SBar.ActualHeight - SystemParameters.ScrollWidth;

            // add vertical lines
            for (int i = 0; i <= Viewport.Width; i += EDIT_SIZE)
            {
                var line = new Line
                {
                    Stroke = Brushes.Black,
                    X1 = i,
                    X2 = i,
                    Y1 = 0,
                    Y2 = Viewport.Height
                };

                line.Stroke.Freeze();
                Viewport.Children.Add(line);
            }

            // add horizontal lines
            for (int i = 0; i <= Viewport.Height; i += EDIT_SIZE)
            {
                var line = new Line
                {
                    Stroke = Brushes.Black,
                    X1 = 0,
                    X2 = Viewport.Width,
                    Y1 = i,
                    Y2 = i
                };

                line.Stroke.Freeze();
                Viewport.Children.Add(line);
            }

            // if there are cells (for edit mode) add these cells
            foreach (var ellipse in editCells)
                Viewport.Children.Add(ellipse);
        }


        /// <summary>
        /// Viewport mouse down event.
        /// </summary>
        private void Viewport_MouseDown(object sender, MouseButtonEventArgs e)
        {
            lock (mutex)
            {
                // returns if we are not in edit mode
                if (!editMode)
                    return;
            }

            // calculates the abscissa and ordinate of the ellipse
            var pos = e.GetPosition(Viewport);
            int x = (int)(pos.X / EDIT_SIZE) * EDIT_SIZE;
            int y = (int)(pos.Y / EDIT_SIZE) * EDIT_SIZE;

            Ellipse ellipse = null;

            // is there already an ellipse in this point?
            foreach (var elm in Viewport.Children.OfType<Ellipse>())
            {
                if (x == Canvas.GetLeft(elm) && y == Canvas.GetTop(elm))
                {
                    // ellipse found
                    ellipse = elm;
                    break;
                }
            }

            // if an ellipse already exist in this poit it will be deleted, otherwise the ellipse will be added to the viewport
            if (ellipse != null)
            {
                // remove the ellipse
                editCells.Remove(ellipse);
                Viewport.Children.Remove(ellipse);
            }
            else
            {
                var cell = GetCellShape(EDIT_SIZE);

                // set ellipse's coordinates
                Canvas.SetLeft(cell, x);
                Canvas.SetTop(cell, y);

                // add the new ellipse
                editCells.Add(cell);
                Viewport.Children.Add(cell);
            }

            // enable/disable save mode
            if (editCells.Count > 0)
            {
                MItemSave.IsEnabled = true;
                MItemPauseResume.IsEnabled = true;
            }
            else
            {
                MItemPauseResume.IsEnabled = false;
                MItemSave.IsEnabled = false;
            }
        }


        /// <summary>
        /// Enter in edit mode.
        /// </summary>
        private void EditMode()
        {
            lock (mutex)
            {
                editMode = true;
                stopTimer = true;
            }

            // stop update
            timer.Stop();

            Title = "Life";
            MItemPauseResume.Header = "Run";
            MItemPauseResume.IsEnabled = false;
            MItemSpeed.IsEnabled = false;
            MItemZoomMinus.IsEnabled = false;
            MItemZoomPlus.IsEnabled = false;
            MItemSave.IsEnabled = false;

            // clear edit mode cells
            editCells.Clear();
            TBlockGeneraton.Text = null;

            // change scrollbars positions
            SViewer.ScrollToVerticalOffset(0);
            SViewer.ScrollToHorizontalOffset(0);

            // draw the grid
            DrawGrid();
        }


        /// <summary>
        /// Enter in run mode.
        /// </summary>
        /// <param name="size">Viewport size.</param>
        private void RunMode(Size size)
        {
            // clear the viewport
            Viewport.Children.Clear();

            // clear cells
            cells.Clear();
            editCells.Clear();

            // reset update speed
            timer.Interval = TimeSpan.FromMilliseconds(TIME_NORMAL);
            MItemSlow.IsChecked = false;
            MItemNormal.IsChecked = true;
            MItemFast.IsChecked = false;
            MItemVeryFast.IsChecked = false;

            // set viewport size
            Viewport.Width = size.Width;
            Viewport.Height = size.Height;

            // compute shifts
            shiftX = Viewport.Width / 2;
            shiftY = Viewport.Height / 2;

            // let scrollbars to change posizion
            lock (mutex)
                initScrollBar = true;

            // enable menu items
            MItemPauseResume.IsEnabled = true;
            MItemSpeed.IsEnabled = true;
            MItemZoomMinus.IsEnabled = true;
            MItemZoomPlus.IsEnabled = true;
        }



        #region MenuItem Events



        /// <summary>
        /// When the menu is open, the population update is stopped.
        /// </summary>
        private void MItem_SubmenuOpened(object sender, RoutedEventArgs e)
        {
            Debug.Assert(timer != null);
            timer.Stop();
        }


        /// <summary>
        /// Continue to update population if menu are closed.
        /// </summary>
        private void MItem_SubmenuClosed(object sender, RoutedEventArgs e)
        {
            lock (mutex)
            {
                // returs if timer is blocked from another operation
                if (stopTimer)
                    return;
            }

            Debug.Assert(timer != null);

            // check if there are open submenus
            foreach (var item in Menu.Items)
            {
                if ((item as MenuItem).IsSubmenuOpen)
                    return;
            }

            // if the population is not die out resume the update
            if (universe.Count > 0)
                timer.Start();
        }


        /// <summary>
        /// Update speed slow.
        /// </summary>
        private void MItemSlow_Click(object sender, RoutedEventArgs e)
        {
            // set the new interval
            Debug.Assert(timer != null);
            timer.Interval = TimeSpan.FromMilliseconds(TIME_SLOW);

            // Mutual exclusion speed selection
            MItemSlow.IsChecked = true;
            MItemNormal.IsChecked = false;
            MItemFast.IsChecked = false;
            MItemVeryFast.IsChecked = false;

            lock (mutex)
            {
                // if the update has not been suspended signal the update can continue
                if (paused)
                    stopTimer = true;
            }            
        }


        /// <summary>
        /// Update speed normal.
        /// </summary>
        private void MItemNormal_Click(object sender, RoutedEventArgs e)
        {
            // set the new interval
            Debug.Assert(timer != null);
            timer.Interval = TimeSpan.FromMilliseconds(TIME_NORMAL);

            // Mutual exclusion speed selection
            MItemSlow.IsChecked = false;
            MItemNormal.IsChecked = true;
            MItemFast.IsChecked = false;
            MItemVeryFast.IsChecked = false;

            lock (mutex)
            {
                // if the update has not been suspended signal the update can continue
                if (paused)
                    stopTimer = true;
            }
        }


        /// <summary>
        /// Update speed fast.
        /// </summary>
        private void MItemFast_Click(object sender, RoutedEventArgs e)
        {
            // set the new interval
            Debug.Assert(timer != null);
            timer.Interval = TimeSpan.FromMilliseconds(TIME_FAST);

            // Mutual exclusion speed selection
            MItemSlow.IsChecked = false;
            MItemNormal.IsChecked = false;
            MItemFast.IsChecked = true;
            MItemVeryFast.IsChecked = false;

            lock (mutex)
            {
                // if the update has not been suspended signal the update can continue
                if (paused)
                    stopTimer = true;
            }
        }


        /// <summary>
        /// Update speed very fast.
        /// </summary>
        private void MItemVeryFast_Click(object sender, RoutedEventArgs e)
        {
            // set the new interval
            Debug.Assert(timer != null);
            timer.Interval = TimeSpan.FromMilliseconds(TIME_VERY_FAST);

            // Mutual exclusion speed selection
            MItemSlow.IsChecked = false;
            MItemNormal.IsChecked = false;
            MItemFast.IsChecked = false;
            MItemVeryFast.IsChecked = true;

            lock (mutex)
            {
                // if the update has not been suspended signal the update can continue
                if (paused)
                    stopTimer = true;
            }
        }


        /// <summary>
        /// Save current population.
        /// </summary>
        private void MItemSave_Click(object sender, RoutedEventArgs e)
        {
            // prevent to update the population while saving 
            lock (mutex)
                stopTimer = true;

            var sfd = new SaveFileDialog
            {
                Filter = "Life File (*.xml)|*.xml|Show All Files (*.*)|*.*",
                FileName = DateTime.Now.ToString("G").Replace("/", "_").Replace(":", "_"),
                Title = "Salva come",
                InitialDirectory = Directory.GetCurrentDirectory(),
            };

            if (sfd.ShowDialog().Value)
            {
                lock (mutex)
                {
                    // save either the edit mode population or the current population
                    if (editMode)
                    {
                        universe.Clear();
                        var points = editCells.Select(el => new Point(Canvas.GetLeft(el) / EDIT_SIZE, Canvas.GetTop(el) / EDIT_SIZE));
                        universe.Load(points);
                        universe.Save(sfd.FileName, new Size(MAX_SIZE, MAX_SIZE));
                        universe.Clear();
                    }
                    else
                        universe.Save(sfd.FileName, new Size(Viewport.ActualWidth, Viewport.ActualHeight));
                }
            }

            lock (mutex)
            {
                stopTimer = false;

                // if the update has been suspended signal the update can't continue
                if (paused)
                    return;
            }

            // if the population is not die out resume the update
            if (universe.Count > 0)
                timer.Start();
        }


        /// <summary>
        /// Load a population from a file.
        /// </summary>
        private void MItemLoad_Click(object sender, RoutedEventArgs e)
        {
            // prevent to update the population while loading 
            lock (mutex)
                stopTimer = true;

            var ofd = new OpenFileDialog
            {
                Filter = "Life File (*.xml)|*.xml|Show All Files (*.*)|*.*",
                InitialDirectory = Directory.GetCurrentDirectory(),
            };

            // load the new file
            if (ofd.ShowDialog().Value && universe.Load(ofd.FileName) != null)
            {
                // enters in edit mode
                EditMode();

                // change window title
                Title = String.Format("Life - {0}", System.IO.Path.GetFileNameWithoutExtension(ofd.FileName));

                // Draw the cells
                foreach (var pt in universe.Population)
                {
                    var cell = GetCellShape(EDIT_SIZE);

                    // set ellipse's coordinates
                    Canvas.SetLeft(cell, pt.X * EDIT_SIZE);
                    Canvas.SetTop(cell, pt.Y * EDIT_SIZE);

                    // add the new ellipse
                    editCells.Add(cell);
                    Viewport.Children.Add(cell);
                }

                if (editCells.Count > 0)
                    MItemPauseResume.IsEnabled = true;
            }
            else
            {
                lock (mutex)
                {
                    stopTimer = false;

                    // if the update has been suspended signal the update can't continue
                    if (paused)
                        return;
                }
                
                // if the population is not die out resume the update
                if (universe.Count > 0)
                    timer.Start();
            }
        }


        /// <summary>
        /// Runs an istance of the game.
        /// </summary>
        private void MItemRun_Click(object sender, RoutedEventArgs e)
        {
            // prevent to update the population while loading 
            lock (mutex)
                stopTimer = true;

            var ofd = new OpenFileDialog
            {
                Filter = "Life File (*.xml)|*.xml|Show All Files (*.*)|*.*",
                InitialDirectory = Directory.GetCurrentDirectory(),
            };

            Size? size;

            // load the new file
            if (ofd.ShowDialog().Value && (size = universe.Load(ofd.FileName)) != null)
            {
                lock (mutex)
                    editMode = false;

                // change window title
                Title = String.Format("Life - {0}", System.IO.Path.GetFileNameWithoutExtension(ofd.FileName));

                // enters in run mode
                RunMode(size.Value);
            }

            lock (mutex)
                stopTimer = false;

            MItemPauseResume.Header = "Pause";
            MItemPauseResume.IsEnabled = true;

            // if the population is not die out resume the update
            if (universe.Count > 0)
                timer.Start();
        }


        /// <summary>
        /// Pause/Resume population update.
        /// </summary>
        private void MItemPauseResume_Click(object sender, RoutedEventArgs e)
        {
            if (timer.IsEnabled)
            {
                // stop the update
                timer.Stop();

                lock (mutex)
                    paused = true;

                MItemPauseResume.Header = "Resume";
            }
            else
            {
                lock (mutex)
                {
                    // save either the edit mode population or the current population
                    if (editMode)
                    {
                        editMode = false;
                        universe.Clear();
                        var points = editCells.Select(el => new Point(Canvas.GetLeft(el) / EDIT_SIZE, Canvas.GetTop(el) / EDIT_SIZE));
                        universe.Load(points);
                        RunMode(new Size(MAX_SIZE, MAX_SIZE));
                    }
                }

                // resume the update
                timer.Start();

                lock (mutex)
                {
                    stopTimer = false;
                    paused = false;
                }

                MItemPauseResume.Header = "Pause";
            }
        }


        /// <summary>
        /// Increases zoom.
        /// </summary>
        private void ZoomPlus_Click(object sender, RoutedEventArgs e)
        {
            IncreaseZoom();

            // update cells size
            foreach (var cell in cells)
                cell.Width = cell.Height = currentZoom;

            MItemZoomMinus.IsEnabled = true;

            if (currentZoom == MAX_ZOOM)
                MItemZoomPlus.IsEnabled = false;

            lock (mutex)
            {
                if (paused)
                    stopTimer = true;
            }
        }


        /// <summary>
        /// Decreases zoom.
        /// </summary>
        private void ZoomMinus_Click(object sender, RoutedEventArgs e)
        {
            DecreaseZoom();

            // aggiorna la dimensione delle cellule
            foreach (var cell in cells)
                cell.Width = cell.Height = currentZoom;

            MItemZoomPlus.IsEnabled = true;

            if (currentZoom == MIM_ZOOM)
                MItemZoomMinus.IsEnabled = false;

            lock (mutex)
            {
                if (paused)
                    stopTimer = true;
            }
        }
        

        /// <summary>
        /// Chenge cells fill color.
        /// </summary>
        private void MItemCellColor_Click(object sender, RoutedEventArgs e)
        {
            lock (mutex)
                stopTimer = true;

            var fcd = new System.Windows.Forms.ColorDialog();

            if (fcd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                cellFill = new SolidColorBrush(Color.FromArgb(255, fcd.Color.R, fcd.Color.G, fcd.Color.B));
                cellFill.Freeze();

                lock (mutex)
                {
                    // update cells fill color
                    if (editMode)
                    {
                        foreach (var cell in editCells)
                            cell.Fill = cellFill;
                    }
                    else
                    {
                        foreach (var cell in cells)
                            cell.Fill = cellFill;
                    }
                }
            }

            lock (mutex)
            {
                if (editMode)
                    return;

                stopTimer = false;

                if (paused)
                    return;
            }

            // if the population is not die out resume the update
            if (universe.Count > 0)
                timer.Start();
        }


        /// <summary>
        /// Entra in edit mode.
        /// </summary>
        private void MItemNew_Click(object sender, RoutedEventArgs e)
        {
            EditMode();
        }

     
        /// <summary>
        /// Quit the process.
        /// </summary>
        private void MItemExit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }


        /// <summary>
        /// Display about window.
        /// </summary>
        private void MItemAbout_Click(object sender, RoutedEventArgs e)
        {
            bool stop = timer.IsEnabled;

            if (stop)
                timer.Stop();

            var about = new AboutWindow();
            about.ShowDialog();

            if (stop)
                timer.Start();
        }


        #endregion

    }
}
