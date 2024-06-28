using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using OpenEphys.ProbeInterface;
using ZedGraph;

namespace OpenEphys.Onix.Design
{
    public partial class Rhs2116ChannelConfigurationDialog : ChannelConfigurationDialog
    {
        //public readonly List<Rhs2116Electrode> Electrodes;
        //public readonly List<Rhs2116Electrode> ChannelMap;

        public Rhs2116ChannelConfigurationDialog(Rhs2116ProbeGroup probeGroup)
            : base(probeGroup)
        {
            InitializeComponent();
            ChannelConfiguration = probeGroup;

            zedGraphChannels.ZoomButtons = MouseButtons.None;
            zedGraphChannels.ZoomButtons2 = MouseButtons.None;

            //ChannelMap = DesignHelper.ToChannelMap((NeuropixelsV1eProbeGroup)ChannelConfiguration);
            //Electrodes = DesignHelper.ToElectrodes((NeuropixelsV1eProbeGroup)ChannelConfiguration);

            HighlightEnabledContacts();
            DrawContactLabels();
            RefreshZedGraph();
        }

        public override ProbeGroup DefaultChannelLayout()
        {
            return new Rhs2116ProbeGroup();
        }
        internal override void LoadDefaultChannelLayout()
        {
            base.LoadDefaultChannelLayout();

            //DesignHelper.UpdateElectrodes(Electrodes, (NeuropixelsV1eProbeGroup)ChannelConfiguration);
            //DesignHelper.UpdateChannelMap(ChannelMap, (NeuropixelsV1eProbeGroup)ChannelConfiguration);

            //OnFileOpenHandler();
        }

        internal override void OpenFile<T>()
        {
            base.OpenFile<NeuropixelsV1eProbeGroup>();

            //DesignHelper.UpdateChannelMap(ChannelMap, (NeuropixelsV1eProbeGroup)ChannelConfiguration);

            OnFileOpenHandler();
        }

        private void OnFileOpenHandler()
        {
            //OnFileLoad?.Invoke(this, EventArgs.Empty);
        }

        private void OnZoomHandler()
        {
            //OnZoom?.Invoke(this, EventArgs.Empty);
        }

        //public override void HighlightEnabledContacts()
        //{
        //    if (Electrodes == null || Electrodes.Count == 0)
        //        return;

        //    foreach (var e in Electrodes)
        //    {
        //        var tag = string.Format(ContactStringFormat, 0, e.ElectrodeNumber);

        //        var fillColor = ChannelMap[e.Channel].ElectrodeNumber == e.ElectrodeNumber ?
        //                        (ReferenceContacts.Any(x => x == e.ElectrodeNumber) ? ReferenceContactFill : EnabledContactFill) :
        //                        DisabledContactFill;

        //        if (zedGraphChannels.GraphPane.GraphObjList[tag] is BoxObj graphObj)
        //        {
        //            graphObj.Fill.Color = fillColor;
        //        }
        //        else
        //        {
        //            throw new NullReferenceException($"Tag {tag} is not found in the graph object list");
        //        }
        //    }
        //}

        // HERE: Use this logic to highlight the invalid channels above

        //private void VisualizeSelectedChannels()
        //{
        //    bool showAllChannels = SelectedChannels.All(x => x == false);

        //    for (int i = 0; i < SelectedChannels.Length; i++)
        //    {
        //        EllipseObj circleObj = (EllipseObj)zedGraphChannels.GraphPane.GraphObjList[string.Format(ChannelConfigurationDialog.ContactStringFormat, i)];

        //        if (circleObj != null)
        //        {
        //            if (!Sequence.Stimuli[i].IsValid())
        //            {
        //                circleObj.Fill.Color = Color.Red;
        //            }
        //            else if (showAllChannels || !SelectedChannels[i])
        //            {
        //                circleObj.Fill.Color = Color.White;
        //            }
        //            else
        //            {
        //                circleObj.Fill.Color = Color.SlateGray;
        //            }
        //        }
        //    }

        //    zedGraphChannels.Refresh();
        //}


        //public override void DrawContactLabels()
        //{
        //    if (Electrodes == null || Electrodes.Count == 0)
        //        return;

        //    var fontSize = CalculateFontSize();

        //    foreach (var e in Electrodes)
        //    {
        //        string id = ChannelMap[e.Channel].ElectrodeNumber == e.ElectrodeNumber ? e.ElectrodeNumber.ToString() : "Off";

        //        TextObj textObj = new(id, e.Position.X, e.Position.Y)
        //        {
        //            ZOrder = ZOrder.A_InFront,
        //            Tag = string.Format(TextStringFormat, 0, e.ElectrodeNumber)
        //        };

        //        SetTextObj(textObj, fontSize);

        //        zedGraphChannels.GraphPane.GraphObjList.Add(textObj);
        //    }
        //}

        //public void EnableElectrodes(List<NeuropixelsV1eElectrode> electrodes)
        //{
        //    ChannelMap.SelectElectrodes(electrodes);
        //}

    }
}
