﻿using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ZedGraph;
using System;
using OpenEphys.ProbeInterface;

namespace OpenEphys.Onix.Design
{
    /// <summary>
    /// Simple dialog window that serves as the base class for all Channel Configuration windows.
    /// Within, there are a number of useful methods for initializing, resizing, and drawing channels.
    /// Each device must implement their own ChannelConfigurationDialog.
    /// </summary>
    public abstract partial class ChannelConfigurationDialog : Form
    {
        /// <summary>
        /// Standardize the format of the string used for creating tags, so that
        /// they can be searched for effectively
        /// </summary>
        public const string ContactStringFormat = "Contact_{0}";
        /// <summary>
        /// Standardize the format of the string used for creating tags, so that
        /// they can be searched for effectively
        /// </summary>
        public const string TextStringFormat = "TextContact_{0}";

        /// <summary>
        /// Local variable that holds the channel configuration in memory until the user presses Okay
        /// </summary>
        public ProbeGroup ChannelConfiguration;

        readonly Color InactiveContactColor = Color.DarkGray;
        readonly Color ActiveContactColor = Color.LightYellow;

        /// <summary>
        /// Constructs the dialog window using the given probe group, and plots all contacts after loading.
        /// </summary>
        /// <param name="probeGroup">Channel configuration given as a <see cref="ProbeGroup"/></param>
        public ChannelConfigurationDialog(ProbeGroup probeGroup)
        {
            InitializeComponent();
            Shown += FormShown;

            if (probeGroup == null)
            {
                LoadDefaultChannelLayout();
            }
            else
            {
                ChannelConfiguration = DefaultChannelLayout();
            }

            InitializeZedGraphChannels();
            DrawChannels();
        }

        /// <summary>
        /// Return the default channel layout of the current device, which fully instatiates the probe group object
        /// </summary>
        /// <example>
        /// Using a class that inherits from ProbeGroup, the general usage would
        /// be the default constructor which should fully initialize a <see cref="ProbeGroup"/> object.
        /// For example, if there was <code>SampleDeviceProbeGroup : ProbeGroup</code>, the body of this 
        /// function could be:
        /// <code>
        /// return new SampleDeviceProbeGroup();
        /// </code>
        /// </example>
        /// <returns>Returns an object that inherits from <see cref="ProbeGroup"/></returns>
        public abstract ProbeGroup DefaultChannelLayout();

        /// <summary>
        /// After every zoom event, check that the axis liimits are equal to maintain the equal
        /// aspect ratio of the graph, ensuring that all contacts do not look smashed or stretched.
        /// </summary>
        /// <param name="sender">Incoming <see cref="ZedGraphControl"/> object</param>
        /// <param name="oldState"><code>null</code></param>
        /// <param name="newState">New state, of type <see cref="ZoomState"/></param>
        public virtual void ZoomEvent(ZedGraphControl sender, ZoomState oldState, ZoomState newState)
        {
            if (newState.Type == ZoomState.StateType.Zoom || newState.Type == ZoomState.StateType.WheelZoom)
            {
                var rangeX = sender.GraphPane.XAxis.Scale.Max - sender.GraphPane.XAxis.Scale.Min;
                var rangeY = sender.GraphPane.YAxis.Scale.Max - sender.GraphPane.YAxis.Scale.Min;

                if (rangeX > rangeY)
                {
                    var diff = rangeX - rangeY;

                    sender.GraphPane.YAxis.Scale.Max += diff / 2;
                    sender.GraphPane.YAxis.Scale.Min -= diff / 2;
                }
                else if (rangeX < rangeY) 
                {
                    var diff = rangeY - rangeX;

                    sender.GraphPane.XAxis.Scale.Max += diff / 2;
                    sender.GraphPane.XAxis.Scale.Min -= diff / 2;
                }
            }
        }

        private void FormShown(object sender, EventArgs e)
        {
            if (!TopLevel)
            {
                splitContainer1.Panel2Collapsed = true;
                splitContainer1.Panel2.Hide();

                menuStrip.Visible = false;
            }

            UpdateFontSize();
            zedGraphChannels.Refresh();
        }

        private void LoadDefaultChannelLayout()
        {
            ChannelConfiguration = DefaultChannelLayout();
        }

        private void OpenFile()
        {
            using OpenFileDialog ofd = new();

            ofd.Filter = "Probe Interface Files (*.json)|*.json";
            ofd.FilterIndex = 1;
            ofd.Multiselect = false;
            ofd.Title = "Choose probe interface file";

            if (ofd.ShowDialog() == DialogResult.OK && File.Exists(ofd.FileName))
            {
                var channelConfiguration = DesignHelper.DeserializeString<ProbeGroup>(File.ReadAllText(ofd.FileName));

                if (channelConfiguration == null || channelConfiguration.NumContacts != 32)
                {
                    MessageBox.Show("Error opening the JSON file. Incorrect number of contacts.");
                    return;
                }
                else
                {
                    ChannelConfiguration = channelConfiguration;
                }
            }
        }

        /// <summary>
        /// Draw all available contacts in the probe contour, with the device channel indices plotted to indicate the contact number.
        /// </summary>
        public void DrawChannels()
        {
            if (ChannelConfiguration == null)
                return;

            zedGraphChannels.GraphPane.GraphObjList.Clear();

            for (int i = 0; i < ChannelConfiguration.Probes.Count(); i++)
            {
                PointD[] planarContours = ConvertFloatArrayToPointD(ChannelConfiguration.Probes.ElementAt(i).ProbePlanarContour);
                PolyObj contour = new(planarContours, Color.Black, Color.White)
                {
                    ZOrder = ZOrder.C_BehindChartBorder
                };

                zedGraphChannels.GraphPane.GraphObjList.Add(contour);

                for (int j = 0; j < ChannelConfiguration.Probes.ElementAt(i).ContactPositions.Length; j++)
                {
                    Contact contact = ChannelConfiguration.Probes.ElementAt(i).GetContact(j);

                    Color color = contact.DeviceId == -1 ? InactiveContactColor : ActiveContactColor;
                    string id =   contact.DeviceId == -1 ? "Off"                : contact.DeviceId.ToString();

                    if (contact.Shape.Equals(ContactShape.Circle))
                    {
                        var size = contact.ShapeParams.Radius.Value * 2;

                        EllipseObj contactObj = new(contact.PosX - size / 2, contact.PosY + size / 2,
                            size, size, Color.DarkGray, color)
                        {
                            ZOrder = ZOrder.B_BehindLegend,
                            Tag = string.Format(ContactStringFormat, contact.Index)
                        };

                        zedGraphChannels.GraphPane.GraphObjList.Add(contactObj);
                    }
                    else if (contact.Shape.Equals(ContactShape.Square))
                    {
                        var size = contact.ShapeParams.Width.Value;

                        BoxObj contactObj = new(contact.PosX - size / 2, contact.PosY + size / 2,
                            size, size, Color.DarkGray, color)
                        {
                            ZOrder = ZOrder.B_BehindLegend,
                            Tag = string.Format(ContactStringFormat, contact.Index)
                        };

                        zedGraphChannels.GraphPane.GraphObjList.Add(contactObj);
                    }
                    else
                    {
                        MessageBox.Show("Contact shapes other than 'circle' and 'square' not implemented yet.");
                        return;
                    }

                    TextObj textObj = new(id, contact.PosX, contact.PosY)
                    {
                        ZOrder = ZOrder.A_InFront,
                        Tag = string.Format(TextStringFormat, contact.Index)
                    };
                    textObj.FontSpec.IsBold = true;
                    textObj.FontSpec.Border.IsVisible = false;
                    textObj.FontSpec.Fill.IsVisible = false;

                    zedGraphChannels.GraphPane.GraphObjList.Add(textObj);
                }
            }

            DrawScale();

            zedGraphChannels.Refresh();
        }

        public virtual void DrawScale()
        {
        }

        internal void UpdateFontSize()
        {
            var fontSize = CalculateFontSize();

            foreach (var obj in zedGraphChannels.GraphPane.GraphObjList)
            {
                if (obj == null) continue;

                if (obj is TextObj textObj)
                {
                    textObj.FontSpec.Size = fontSize;
                }
            }
        }

        internal float CalculateFontSize()
        {
            float rangeY = (float)(zedGraphChannels.GraphPane.YAxis.Scale.Max - zedGraphChannels.GraphPane.YAxis.Scale.Min);

            float contactSize = ContactSize();

            var fontSize = 300f * contactSize / rangeY;

            fontSize = fontSize < 1f ? 1f : fontSize;
            fontSize = fontSize > 100f ? 200f : fontSize;

            return fontSize;
        }

        internal float ContactSize()
        {
            var obj = zedGraphChannels.GraphPane.GraphObjList
                        .OfType<BoxObj>()
                        .Where(obj => obj is not PolyObj)
                        .FirstOrDefault();

            if (obj != null && obj != default(BoxObj))
            {
                return (float)obj.Location.Width;
            }

            return 1f;
        }

        /// <summary>
        /// After a resize event (such as changing the window size), readjust the size of the control to 
        /// ensure an equal aspect ratio for axes.
        /// </summary>
        public void ResizeAxes()
        {
            SetEqualAspectRatio();

            RectangleF axisRect = zedGraphChannels.GraphPane.Rect;

            if (axisRect.Width > axisRect.Height)
            {
                axisRect.X += (axisRect.Width - axisRect.Height) / 2;
                axisRect.Width = axisRect.Height;
            }
            else if (axisRect.Height > axisRect.Width)
            {
                axisRect.Y += (axisRect.Height - axisRect.Width) / 2;
                axisRect.Height = axisRect.Width;
            }
            else
            {
                zedGraphChannels.GraphPane.Chart.Rect = axisRect;
                return;
            }

            zedGraphChannels.GraphPane.Rect = axisRect;
            zedGraphChannels.GraphPane.Chart.Rect = axisRect;

            zedGraphChannels.Size = new Size((int)axisRect.Width, (int)axisRect.Height);
            zedGraphChannels.Location = new Point((int)axisRect.X, (int)axisRect.Y);
        }

        internal void SetEqualAspectRatio()
        {
            if (zedGraphChannels.GraphPane.GraphObjList.Count == 0)
                return;

            var minX = MinX(zedGraphChannels.GraphPane.GraphObjList);
            var minY = MinY(zedGraphChannels.GraphPane.GraphObjList);
            var maxX = MaxX(zedGraphChannels.GraphPane.GraphObjList);
            var maxY = MaxY(zedGraphChannels.GraphPane.GraphObjList);

            var rangeX = maxX - minX;
            var rangeY = maxY - minY;

            if (rangeY < rangeX)
            {
                var diff = (rangeX - rangeY) / 2;
                minY -= diff;
                maxY += diff;
            }
            else
            {
                var diff = (rangeY - rangeX) / 2;
                minX -= diff;
                maxX += diff;
            }

            zedGraphChannels.GraphPane.XAxis.Scale.Min = minX;
            zedGraphChannels.GraphPane.XAxis.Scale.Max = maxX;

            zedGraphChannels.GraphPane.YAxis.Scale.Min = minY;
            zedGraphChannels.GraphPane.YAxis.Scale.Max = maxY;
        }

        protected static double MinX(GraphObjList graphObjs)
        {
            return graphObjs.Min<GraphObj, double>(obj =>
            {
                if (obj is PolyObj polyObj)
                {
                    return polyObj.Points.Min(p => p.X);
                }

                return double.MaxValue;
            });
        }

        protected static double MinY(GraphObjList graphObjs)
        {
            return graphObjs.Min<GraphObj, double>(obj =>
            {
                if (obj is PolyObj polyObj)
                {
                    return polyObj.Points.Min(p => p.Y);
                }

                return double.MaxValue;
            });
        }

        protected static double MaxX(GraphObjList graphObjs)
        {
            return graphObjs.Max<GraphObj, double>(obj =>
            {
                if (obj is PolyObj polyObj)
                {
                    return polyObj.Points.Max(p => p.X);
                }

                return double.MinValue;
            });
        }

        protected static double MaxY(GraphObjList graphObjs)
        {
            return graphObjs.Max<GraphObj, double>(obj =>
            {
                if (obj is PolyObj polyObj)
                {
                    return polyObj.Points.Max(p => p.Y);
                }

                return double.MinValue;
            });
        }

        /// <summary>
        /// Converts a two-dimensional <see cref="float"/> array into an array of <see cref="PointD"/>
        /// objects. Assumes that the float array is ordered so that the first index of each pair is 
        /// the X position, and the second index is the Y position.
        /// </summary>
        /// <param name="floats">Two-dimensional array of <see cref="float"/> values</param>
        /// <returns></returns>
        public static PointD[] ConvertFloatArrayToPointD(float[][] floats)
        {
            PointD[] pointD = new PointD[floats.Length];

            for (int i = 0; i < floats.Length; i++)
            {
                pointD[i] = new PointD(floats[i][0], floats[i][1]);
            }

            return pointD;
        }

        /// <summary>
        /// Initialize the given <see cref="ZedGraphControl"/> so that almost everything other than the 
        /// axis itself is hidden, reducing visual clutter before plotting contacts
        /// </summary>
        public void InitializeZedGraphChannels()
        {
            zedGraphChannels.GraphPane.Title.IsVisible = false;
            zedGraphChannels.GraphPane.TitleGap = 0;
            zedGraphChannels.GraphPane.Border.IsVisible = false;
            zedGraphChannels.GraphPane.Border.Width = 0;
            zedGraphChannels.GraphPane.Chart.Border.IsVisible = false;
            zedGraphChannels.GraphPane.Margin.All = -1;
            zedGraphChannels.GraphPane.IsFontsScaled = true;
            zedGraphChannels.BorderStyle = BorderStyle.None;

            zedGraphChannels.GraphPane.XAxis.IsVisible = false;
            zedGraphChannels.GraphPane.XAxis.IsAxisSegmentVisible = false;
            zedGraphChannels.GraphPane.XAxis.Scale.MaxAuto = true;
            zedGraphChannels.GraphPane.XAxis.Scale.MinAuto = true;

            zedGraphChannels.GraphPane.YAxis.IsVisible = false;
            zedGraphChannels.GraphPane.YAxis.IsAxisSegmentVisible = false;
            zedGraphChannels.GraphPane.YAxis.Scale.MaxAuto = true;
            zedGraphChannels.GraphPane.YAxis.Scale.MinAuto = true;
        }

        private void MenuItemSaveFile_Click(object sender, EventArgs e)
        {
            using SaveFileDialog sfd = new();
            sfd.Filter = "Probe Interface Files (*.json)|*.json";
            sfd.FilterIndex = 1;
            sfd.Title = "Choose where to save the probe interface file";
            sfd.OverwritePrompt = true;
            sfd.ValidateNames = true;

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                DesignHelper.SerializeObject(ChannelConfiguration, sfd.FileName);
            }
        }

        private void ZedGraphChannels_Resize(object sender, EventArgs e)
        {
            ResizeAxes();
            UpdateFontSize();
            zedGraphChannels.AxisChange();
            zedGraphChannels.Refresh();
        }

        private void MenuItemOpenFile_Click(object sender, EventArgs e)
        {
            OpenFile();
            DrawChannels();
        }

        private void LoadDefaultToolStripMenuItem_Click(object sender, EventArgs e)
        {
            LoadDefaultChannelLayout();
            DrawChannels();
        }

        private void ButtonOK_Click(object sender, EventArgs e)
        {
            if (TopLevel)
            {
                DialogResult = DialogResult.OK;
                Close();
            }
        }

        public void ManualZoom(double zoomFactor)
        {
            var center = new PointF(zedGraphChannels.GraphPane.Rect.Left + zedGraphChannels.GraphPane.Rect.Width / 2,
                                    zedGraphChannels.GraphPane.Rect.Top  + zedGraphChannels.GraphPane.Rect.Height / 2);

            zedGraphChannels.ZoomPane(zedGraphChannels.GraphPane, 1 / zoomFactor, center, true);

            UpdateFontSize();
        }

        public void ResetZoom()
        {
            SetEqualAspectRatio();
            UpdateFontSize();
        }

        /// <summary>
        /// Shifts the whole ZedGraph to the given relative position, where 0.0 is the very bottom of the horizontal 
        /// space, and 1.0 is the very top. Note that this accounts for a buffer on the top and bottom, so giving a 
        /// value of 0.0 would have the minimum value of Y axis equal to the bottom of the graph, and keep the range 
        /// the same. Similarly, a value of 1.0 would set the maximum value of the Y axis to the top of the graph, 
        /// and keep the range the same.
        /// </summary>
        /// <param name="relativePosition">A float value defining the percentage of the graph to move to vertically</param>
        public void MoveToVerticalPosition(float relativePosition)
        {
            if (relativePosition < 0.0 || relativePosition > 1.0)
            {
                throw new ArgumentOutOfRangeException(nameof(relativePosition));
            }

            var currentRange = zedGraphChannels.GraphPane.YAxis.Scale.Max - zedGraphChannels.GraphPane.YAxis.Scale.Min;

            var minY = MinY(zedGraphChannels.GraphPane.GraphObjList);
            var maxY = MaxY(zedGraphChannels.GraphPane.GraphObjList);

            var newMinY = (maxY - minY - currentRange) * relativePosition;

            zedGraphChannels.GraphPane.YAxis.Scale.Min = newMinY;
            zedGraphChannels.GraphPane.YAxis.Scale.Max = newMinY + currentRange;
        }

        public void RefreshZedGraph()
        {
            zedGraphChannels.AxisChange();
            zedGraphChannels.Refresh();
        }
    }
}
