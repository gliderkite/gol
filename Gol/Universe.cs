using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Xml;
using System.Xml.Linq;

namespace Life
{
    /// <summary>
    /// Univers of the Game of Life.
    /// </summary>
    public class Universe
    {

        /// <summary>
        /// Living cells.
        /// </summary>
        /// <remarks>The key of the dictionary corresponds to a string composed by the position
        /// of the cell. So, if for example the cell is in position (10, 25) the key will be the
        /// string "10;25" (that is, the string representation of the Point class).</remarks>
        private readonly Dictionary<Point, Point> cells = new Dictionary<Point,Point>();



        /// <summary>
        /// Creates the universe: Big Bang.
        /// </summary>
        public Universe()
        {
            // Init generation number
            Generation = 0;
        }



        /// <summary>
        /// Updates the current population: killing the dyings and adding the unborns.
        /// </summary>
        /// <returns>Returns false if the population is extinct, true otherwise.</returns>
        public bool UpdatePopulation()
        {
            // possible unborns for the next generation
            var unbornsPos = new HashSet<Point>();
            var unborns = new List<Point>();

            // dyings list
            var dyings = new List<Point>();

            // for each living cell
            foreach (KeyValuePair<Point, Point> entry in cells)
            {
                IList<Point> neighbours = GetNeighbours(entry.Value, unbornsPos);

                /* Any live cell with fewer than two live neighbours dies, as if caused by under-population. */
                /* Any live cell with more than three live neighbours dies, as if by overcrowding. */
                if (neighbours.Count < 2 || neighbours.Count > 3)
                    dyings.Add(entry.Key);
            }

            // for each possible unborns
            foreach (Point unborn in unbornsPos)
            {
                IList<Point> neighbours = GetNeighbours(unborn);

                /* Any dead cell with exactly three live neighbours becomes a live cell, as if by reproduction. */
                if (neighbours.Count == 3)
                    unborns.Add(unborn);
            }

            // kill dyings
            foreach (Point moribund in dyings)
                cells.Remove(moribund);

            // add unborns
            foreach (Point pt in unborns)
                cells.Add(pt, pt);

            // update generation number
            Generation++;

            return (cells.Count > 0);
        }


        /// <summary>
        /// Gets the current population.
        /// </summary>
        public IList<Point> Population
        {
            get
            {
                return cells.Values.ToList();
            }
        }


        /// <summary>
        /// Number of the current generation.
        /// </summary>
        public uint Generation
        {
            get;
            private set;
        }


        /// <summary>
        /// Gets the number of alive cells.
        /// </summary>
        public int Count
        {
            get
            {
                return cells.Count;
            }
        }


        /// <summary>
        /// Current population bounds.
        /// </summary>
        public Rect Bounds
        {
            get
            {
                if (cells.Count == 0)
                    return new Rect();

                double minX = Double.MaxValue, minY = Double.MaxValue;
                double maxX = Double.MinValue, maxY = Double.MinValue;

                foreach (var entry in cells)
                {
                    Point pt = entry.Value;

                    if (pt.X < minX)
                        minX = pt.X;
                    if (pt.Y < minY)
                        minY = pt.Y;
                    if (pt.X > maxX)
                        maxX = pt.X;
                    if (pt.Y > maxY)
                        maxY = pt.Y;
                }

                return new Rect(minX, minY, Math.Abs(maxX) + Math.Abs(minX), Math.Abs(maxY) + Math.Abs(minY));
            }
        }


        /// <summary>
        /// Saves the current population in a XML file.
        /// </summary>
        /// <param name="filename">File path.</param>
        /// <param name="size">Viewport size.</param>
        /// <returns>Returns a value indicating if the population has been saved correctly.</returns>
        public bool Save(string filename, Size size)
        {
            if (String.IsNullOrEmpty(filename))
                throw new ArgumentNullException("filename");

            try
            {
                // create the XML document
                var doc = new XDocument(new XElement("Life"));
                var root = doc.Element("Life");
                root.Add(new XAttribute("population", Count));
                root.Add(new XAttribute("generation", Generation));
                root.Add(new XAttribute("size", size.ToString(CultureInfo.InvariantCulture)));

                // add all cells
                foreach (KeyValuePair<Point, Point> entry in cells)
                    root.Add(new XElement("Cell") { Value = entry.Value.ToString(CultureInfo.InvariantCulture) });

                // save the document without XML declaration
                var xmlSettings = new XmlWriterSettings { OmitXmlDeclaration = true, Indent = true };
                using (var xw = XmlWriter.Create(filename, xmlSettings))
                    doc.Save(xw);

                return true;
            }
            catch
            {
                return false;
            }
        }


        /// <summary>
        /// Loads the population stored in the XML file.
        /// </summary>
        /// <param name="filename">File path.</param>
        /// <returns>Returns the size of the viewport, or null in case of error.</returns>
        public Size? Load(string filename)
        {
            if (!File.Exists(filename))
                throw new FileNotFoundException(filename);

            try
            {
                var doc = XDocument.Load(filename);
                var root = doc.Element("Life");

                // check if the document is valid
                if (root == null)
                    return null;

                // clear the current population
                cells.Clear();

                // read attributes
                Generation = (uint)root.Attribute("generation");
                Size size = Size.Parse((string)root.Attribute("size"));

                // read cells
                foreach (var cell in root.Elements())
                {
                    Point pt = Point.Parse((string)cell);
                    cells.Add(pt, pt);
                }

                return size;
            }
            catch
            {
                return null;
            }
        }


        /// <summary>
        /// Loads the list of cell specified.
        /// </summary>
        /// <param name="population">Population to load.</param>
        public void Load(IEnumerable<Point> population)
        {
            if (population == null)
                throw new ArgumentNullException("population");

            foreach (var elm in population)
                cells[elm] = elm;
        }


        /// <summary>
        /// Clear the old population.
        /// </summary>
        public void Clear()
        {
            Generation = 0;
            cells.Clear();
        }


        /// <summary>
        /// Get the neighbours of the cell (if there is one).
        /// </summary>
        /// <param name="pos">Position.</param>
        /// <param name="unborns">Possible unborns for the next generation.</param>
        /// <returns></returns>
        private IList<Point> GetNeighbours(Point pos, HashSet<Point> unborns = null)
        {
            var neighbours = new List<Point>();

            // Location of the eight adjacent cells
            Point[] neighboursPos = new Point[]
            {
                new Point(pos.X - 1, pos.Y - 1),    // top-left
                new Point(pos.X, pos.Y - 1),        // top
                new Point(pos.X + 1, pos.Y - 1),    // top-right
                new Point(pos.X - 1, pos.Y),        // left
                new Point(pos.X + 1, pos.Y),        // right
                new Point(pos.X - 1, pos.Y + 1),    // down-left
                new Point(pos.X, pos.Y + 1),        // down
                new Point(pos.X + 1, pos.Y + 1),    // down-right
            };

            // search a living cell for each possible location
            foreach (var pt in neighboursPos)
            {
                Point neighbour;

                // if there is a lining cell add this cell to neighbours, otherwise add this cell to unborns
                if (cells.TryGetValue(pt, out neighbour))
                    neighbours.Add(neighbour);
                else if (unborns != null)
                    unborns.Add(pt);
            }

            return neighbours;
        }


    }
}
