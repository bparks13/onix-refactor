﻿using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ZedGraph;
using System.IO;

namespace OpenEphys.Onix.Design
{
    public partial class Rhs2116StimulusSequenceDialog : Form
    {
        /// <summary>
        /// Holds a local copy of the Rhs2116StimulusSequence until the user presses Okay
        /// </summary>
        public Rhs2116StimulusSequenceDual Sequence;

        public readonly Rhs2116ChannelConfigurationDialog ChannelConfiguration;

        private const double SamplePeriodMicroSeconds = 1e6 / 30.1932367151e3;

        /// <summary>
        /// Opens a dialog allowing for easy changing of stimulus sequence parameters
        /// </summary>
        /// <param name="sequence"></param>
        /// <param name="probeGroup"></param>
        public Rhs2116StimulusSequenceDialog(Rhs2116StimulusSequenceDual sequence, Rhs2116ProbeGroup probeGroup)
        {
            InitializeComponent();
            Shown += FormShown;

            Sequence = new Rhs2116StimulusSequenceDual(sequence);

            propertyGridStimulusSequence.SelectedObject = Sequence;

            comboBoxStepSize.DataSource = Enum.GetValues(typeof(Rhs2116StepSize));
            comboBoxStepSize.SelectedIndex = (int)Sequence.CurrentStepSize;

            if (probeGroup.NumberOfContacts != 32)
            {
                throw new ArgumentException($"Probe group is not valid: 32 channels were expected, there are {probeGroup.NumberOfContacts} instead.");
            }

            ChannelConfiguration = new(probeGroup)
            {
                TopLevel = false,
                FormBorderStyle = FormBorderStyle.None,
                Dock = DockStyle.Fill,
                Parent = this,
            };

            panelProbe.Controls.Add(ChannelConfiguration);
            this.AddMenuItemsFromDialogToFileOption(ChannelConfiguration, "Channel Configuration");

            ChannelConfiguration.Show();

            InitializeZedGraphWaveform();
            DrawStimulusWaveform();

            dataGridViewStimulusTable.DataSource = Sequence.Stimuli;
        }

        private void FormShown(object sender, EventArgs e)
        {
            if (!TopLevel)
            {
                splitContainer2.Panel2Collapsed = true;
                splitContainer2.Panel2.Hide();

                menuStrip.Visible = false;
            }
        }

        private void ButtonOk_Click(object sender, EventArgs e)
        {
            if (TopLevel)
            {
                if (CanCloseForm(Sequence, out DialogResult result))
                {
                    DialogResult = result;
                    Close();
                }
            }
        }

        /// <summary>
        /// Checks the given stimulus sequence for validity, and confirms if the user wants to close the form
        /// </summary>
        /// <param name="sequence">Rhs2116 Stimulus Sequence</param>
        /// <param name="result">DialogResult, used to set the DialogResult of the form before closing</param>
        /// <returns></returns>
        public static bool CanCloseForm(Rhs2116StimulusSequenceDual sequence, out DialogResult result)
        {
            if (sequence != null)
            {
                if (!sequence.Valid)
                {
                    DialogResult resultContinue = MessageBox.Show("Warning: Stimulus sequence is not valid. " +
                        "If you continue, the current settings will be discarded. " +
                        "Press OK to discard changes, or press Cancel to continue editing the sequence.", "Invalid Sequence",
                        MessageBoxButtons.OKCancel);

                    if (resultContinue == DialogResult.OK)
                    {
                        result = DialogResult.Cancel;
                        return true;
                    }
                    else
                    {
                        result = DialogResult.OK;
                        return false;
                    }
                }
                else
                {
                    result = DialogResult.OK;
                    return true;
                }
            }
            else
            {
                result = DialogResult.Cancel;
                return true;
            }
        }

        private void PropertyGridStimulusSequence_PropertyValueChanged(object s, PropertyValueChangedEventArgs e)
        {
            DrawStimulusWaveform();

            // TODO: Add a call to the Rhs2116ChannelConfigurationDialog here to update channels
        }

        private void DrawStimulusWaveform()
        {
            zedGraphWaveform.GraphPane.CurveList.Clear();
            zedGraphWaveform.GraphPane.GraphObjList.Clear();
            zedGraphWaveform.ZoomOutAll(zedGraphWaveform.GraphPane);

            double peakToPeak = (Sequence.MaximumPeakToPeakAmplitudeSteps > 0 ?
                Sequence.CurrentStepSizeuA * Sequence.MaximumPeakToPeakAmplitudeSteps :
                Sequence.CurrentStepSizeuA * 1) * 1.1;

            var stimuli = Sequence.Stimuli;

            zedGraphWaveform.GraphPane.XAxis.Scale.Max = (Sequence.SequenceLengthSamples > 0 ? Sequence.SequenceLengthSamples : 1) * SamplePeriodMicroSeconds;
            zedGraphWaveform.GraphPane.XAxis.Scale.Min = -zedGraphWaveform.GraphPane.XAxis.Scale.Max * 0.03;
            zedGraphWaveform.GraphPane.YAxis.Scale.Min = -peakToPeak * stimuli.Length;
            zedGraphWaveform.GraphPane.YAxis.Scale.Max = peakToPeak;

            var contactTextLocation = zedGraphWaveform.GraphPane.XAxis.Scale.Min / 2;

            for (int i = 0; i < stimuli.Length; i++)
            {
                if (ChannelConfiguration.SelectedContacts[i])
                {
                    PointPairList pointPairs = CreateStimulusWaveform(stimuli[i], -peakToPeak * i);

                    Color color;
                    if (stimuli[i].IsValid())
                    {
                        color = Color.CornflowerBlue;
                    }
                    else
                    {
                        color = Color.Red;
                    }

                    var curve = zedGraphWaveform.GraphPane.AddCurve("", pointPairs, color, SymbolType.None);

                    curve.Label.IsVisible = false;
                    curve.Line.Width = 3;

                    TextObj contactNumber = new(i.ToString(), contactTextLocation, curve.Points[0].Y)
                    {
                        Tag = string.Format(ChannelConfigurationDialog.TextStringFormat, i)
                    };
                    contactNumber.FontSpec.Size = 12;
                    contactNumber.FontSpec.Border.IsVisible = false;
                    contactNumber.FontSpec.Fill.IsVisible = false;

                    zedGraphWaveform.GraphPane.GraphObjList.Add(contactNumber);
                }
            }

            zedGraphWaveform.GraphPane.YAxis.Scale.MinorStep = 0;

            zedGraphWaveform.AxisChange();

            SetStatusValidity();
            SetPercentOfSlotsUsed();

            zedGraphWaveform.Refresh();
        }

        private PointPairList CreateStimulusWaveform(Rhs2116Stimulus stimulus, double yOffset)
        {
            PointPairList points = new()
            {
                { 0, yOffset },
                { stimulus.DelaySamples * SamplePeriodMicroSeconds, yOffset }
            };

            for (int i = 0; i < stimulus.NumberOfStimuli; i++)
            {
                double amplitude = (stimulus.AnodicFirst ? stimulus.AnodicAmplitudeSteps : -stimulus.CathodicAmplitudeSteps) * Sequence.CurrentStepSizeuA + yOffset;
                double width = (stimulus.AnodicFirst ? stimulus.AnodicWidthSamples : stimulus.CathodicWidthSamples) * SamplePeriodMicroSeconds;

                points.Add(points[points.Count - 1].X, amplitude);
                points.Add(points[points.Count - 1].X + width, amplitude);
                points.Add(points[points.Count - 1].X, yOffset);

                points.Add(points[points.Count - 1].X + stimulus.DwellSamples * SamplePeriodMicroSeconds, yOffset);

                amplitude = (stimulus.AnodicFirst ? -stimulus.CathodicAmplitudeSteps : stimulus.AnodicAmplitudeSteps) * Sequence.CurrentStepSizeuA + yOffset;
                width = (stimulus.AnodicFirst ? stimulus.CathodicWidthSamples : stimulus.AnodicWidthSamples) * SamplePeriodMicroSeconds;

                points.Add(points[points.Count - 1].X, amplitude);
                points.Add(points[points.Count - 1].X + width, amplitude);
                points.Add(points[points.Count - 1].X, yOffset);

                points.Add(points[points.Count - 1].X + stimulus.InterStimulusIntervalSamples * SamplePeriodMicroSeconds, yOffset);
            }

            points.Add(Sequence.SequenceLengthSamples * SamplePeriodMicroSeconds, yOffset);

            return points;
        }

        private void InitializeZedGraphWaveform()
        {
            zedGraphWaveform.GraphPane.Title.IsVisible = false;
            zedGraphWaveform.GraphPane.TitleGap = 0;
            zedGraphWaveform.GraphPane.Border.IsVisible = false;
            zedGraphWaveform.GraphPane.IsFontsScaled = false;

            zedGraphWaveform.GraphPane.YAxis.MajorGrid.IsZeroLine = false;

            zedGraphWaveform.GraphPane.XAxis.MajorTic.IsAllTics = false;
            zedGraphWaveform.GraphPane.XAxis.MinorTic.IsAllTics = false;
            zedGraphWaveform.GraphPane.YAxis.MajorTic.IsAllTics = false;
            zedGraphWaveform.GraphPane.YAxis.MinorTic.IsAllTics = false;

            zedGraphWaveform.GraphPane.XAxis.Title.Text = "Time [μs]";
            zedGraphWaveform.GraphPane.YAxis.Title.Text = "Amplitude [μA]";

            zedGraphWaveform.IsAutoScrollRange = true;
        }

        private void SetStatusValidity()
        {
            if (Sequence.Valid && Sequence.FitsInHardware)
            {
                toolStripStatusIsValid.Image = Properties.Resources.StatusReadyImage;
                toolStripStatusIsValid.Text = "Valid stimulus sequence";
            }
            else
            {
                if (!Sequence.FitsInHardware)
                {
                    toolStripStatusIsValid.Image = Properties.Resources.StatusBlockedImage;
                    toolStripStatusIsValid.Text = "Stimulus sequence too complex";
                }
                else
                {
                    toolStripStatusIsValid.Image = Properties.Resources.StatusCriticalImage;
                    toolStripStatusIsValid.Text = "Stimulus sequence not valid";
                }
            }
        }

        private void SetPercentOfSlotsUsed()
        {
            toolStripStatusSlotsUsed.Text = string.Format("{0, 0:P1} of slots used", (double)Sequence.StimulusSlotsRequired / Sequence.MaxMemorySlotsAvailable);
        }

        private void LinkLabelDocumentation_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start("https://open-ephys.github.io/onix-docs/Software%20Guide/Bonsai.ONIX/Nodes/RHS2116TriggerDevice.html");
            }
            catch
            {
                MessageBox.Show("Unable to open documentation link. Please copy and paste the following link " +
                    "manually to find the documentation: " +
                    "https://open-ephys.github.io/onix-docs/Software%20Guide/Bonsai.ONIX/Nodes/RHS2116TriggerDevice.html");
            }
        }

        private void ButtonAddPulses_Click(object sender, EventArgs e)
        {
            if (ChannelConfiguration.SelectedContacts.All(x => x))
            {
                DialogResult result = MessageBox.Show("Caution: All channels are currently selected, and all " +
                    "settings will be applied to all channels if you continue. Press Okay to add pulse settings to all channels, or Cancel to keep them as is",
                    "Set all channel settings?", MessageBoxButtons.OKCancel);

                if (result == DialogResult.Cancel)
                {
                    return;
                }
            }

            for (int i = 0; i < ChannelConfiguration.SelectedContacts.Length; i++)
            {
                if (ChannelConfiguration.SelectedContacts[i])
                {
                    // TODO: Add default values automatically, and display them if they were removed
                    if (delay.Tag == null)
                    {
                        MessageBox.Show("Unable to parse delay.");
                        return;
                    }

                    if (amplitudeAnodic.Tag == null)
                    {
                        MessageBox.Show("Unable to parse anodic amplitude.");
                        return;
                    }

                    if (pulseWidthAnodic.Tag == null)
                    {
                        MessageBox.Show("Unable to parse anodic pulse width.");
                        return;
                    }

                    if (interPulseInterval.Tag == null)
                    {
                        MessageBox.Show("Unable to parse inter-pulse interval.");
                        return;
                    }

                    if (amplitudeCathodic.Tag == null)
                    {
                        MessageBox.Show("Unable to parse cathodic amplitude.");
                        return;
                    }

                    if (pulseWidthCathodic.Tag == null)
                    {
                        MessageBox.Show("Unable to parse cathodic pulse width.");
                        return;
                    }

                    if (interStimulusInterval.Tag == null)
                    {
                        MessageBox.Show("Unable to parse inter-stimulus interval.");
                        return;
                    }

                    if (!uint.TryParse(numberOfStimuli.Text, out uint numberOfStimuliValue))
                    {
                        MessageBox.Show("Unable to parse number of stimuli.");
                        return;
                    }

                    Sequence.Stimuli[i].DelaySamples = (uint)delay.Tag;

                    Sequence.Stimuli[i].AnodicAmplitudeSteps = (byte)amplitudeAnodic.Tag;
                    Sequence.Stimuli[i].AnodicWidthSamples = (uint)pulseWidthAnodic.Tag;

                    Sequence.Stimuli[i].CathodicAmplitudeSteps = (byte)amplitudeCathodic.Tag;
                    Sequence.Stimuli[i].CathodicWidthSamples = (uint)pulseWidthCathodic.Tag;

                    Sequence.Stimuli[i].DwellSamples = (uint)interPulseInterval.Tag;

                    Sequence.Stimuli[i].InterStimulusIntervalSamples = (uint)interStimulusInterval.Tag;

                    Sequence.Stimuli[i].NumberOfStimuli = numberOfStimuliValue;

                    Sequence.Stimuli[i].AnodicFirst = checkBoxAnodicFirst.Checked;
                }
            }

            DrawStimulusWaveform();
            ChannelConfiguration.HighlightEnabledContacts();
        }

        private void ParameterKeyPress_Time(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == '\r')
            {
                Samples_TextChanged(sender, e);
            }
        }

        private void ParameterKeyPress_Amplitude(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == '\r')
            {
                Amplitude_TextChanged(sender, e);
            }
        }

        private void DataGridViewStimulusTable_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            dataGridViewStimulusTable.BindingContext[dataGridViewStimulusTable.DataSource].EndCurrentEdit();
            AddDeviceChannelIndexToGridRow();
            DrawStimulusWaveform();
            ChannelConfiguration.HighlightEnabledContacts();
        }

        private void DataGridViewStimulusTable_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
        {
            AddDeviceChannelIndexToGridRow();
        }

        private void AddDeviceChannelIndexToGridRow()
        {
            if (ChannelConfiguration == null || ChannelConfiguration.GetProbeGroup().NumberOfContacts != 32)
                return;

            var deviceChannelIndices = ChannelConfiguration.GetProbeGroup().GetDeviceChannelIndices();

            for (int i = 0; i < deviceChannelIndices.Count(); i++)
            {
                var index = deviceChannelIndices.ElementAt(i);

                if (index != -1)
                {
                    dataGridViewStimulusTable.Rows[index].HeaderCell.Value = index.ToString();
                }
            }
        }

        private void ComboBoxStepSize_SelectedIndexChanged(object sender, EventArgs e)
        {
            Sequence.CurrentStepSize = (Rhs2116StepSize)comboBoxStepSize.SelectedItem;
            DrawStimulusWaveform();
            UpdateAmplitudeLabelUnits();

            if (amplitudeAnodic.Tag != null)
            {
                amplitudeAnodic.Text = GetAmplitudeString((byte)amplitudeAnodic.Tag);
            }

            if (amplitudeCathodic.Tag != null)
            {
                amplitudeCathodic.Text = GetAmplitudeString((byte)amplitudeCathodic.Tag);
            }
        }

        private string GetAmplitudeString(byte amplitude)
        {
            string format = Sequence.CurrentStepSize switch
            {
                Rhs2116StepSize.Step10nA or Rhs2116StepSize.Step20nA or Rhs2116StepSize.Step50nA => "{0:F2}",
                Rhs2116StepSize.Step100nA or Rhs2116StepSize.Step200nA or Rhs2116StepSize.Step500nA => "{0:F1}",
                Rhs2116StepSize.Step1000nA or Rhs2116StepSize.Step2000nA => "{0:F0}",
                Rhs2116StepSize.Step5000nA => "{0:F3}",
                Rhs2116StepSize.Step10000nA => "{0:F2}",
                _ => "{0:F3}",
            };
            return string.Format(format, GetAmplitudeFromSample(amplitude));
        }

        private string GetTimeString(uint time)
        {
            return string.Format("{0:F2}", GetTimeFromSample(time));
        }

        private double GetUnitConversion()
        {
            return Sequence.CurrentStepSize switch
            {
                Rhs2116StepSize.Step10nA or Rhs2116StepSize.Step20nA or Rhs2116StepSize.Step50nA or
                Rhs2116StepSize.Step100nA or Rhs2116StepSize.Step200nA or Rhs2116StepSize.Step500nA or
                Rhs2116StepSize.Step1000nA or Rhs2116StepSize.Step2000nA => 1,
                Rhs2116StepSize.Step5000nA or Rhs2116StepSize.Step10000nA => 1e3,
                _ => 1e6,
            };
        }

        private void UpdateAmplitudeLabelUnits()
        {
            switch (Sequence.CurrentStepSize)
            {
                case Rhs2116StepSize.Step10nA:
                case Rhs2116StepSize.Step20nA:
                case Rhs2116StepSize.Step50nA:
                case Rhs2116StepSize.Step100nA:
                case Rhs2116StepSize.Step200nA:
                case Rhs2116StepSize.Step500nA:
                case Rhs2116StepSize.Step1000nA:
                case Rhs2116StepSize.Step2000nA:
                    labelAmplitudeAnodic.Text = "Amplitude [μA]";
                    labelAmplitudeCathodic.Text = "Amplitude [μA]";
                    break;

                case Rhs2116StepSize.Step5000nA:
                case Rhs2116StepSize.Step10000nA:
                    labelAmplitudeAnodic.Text = "Amplitude [mA]";
                    labelAmplitudeCathodic.Text = "Amplitude [mA]";
                    break;

                default:
                    labelAmplitudeAnodic.Text = "Amplitude [μA]";
                    labelAmplitudeCathodic.Text = "Amplitude [μA]";
                    break;
            }
        }

        private void Samples_TextChanged(object sender, EventArgs e)
        {
            TextBox textBox = (TextBox)sender;

            if (textBox.Text == "" || textBox.Text == "0" || textBox.Text == "0.0" || textBox.Text == "0.00" || textBox.Text == "0.000")
                return;

            if (double.TryParse(textBox.Text, out double result))
            {
                if (!GetSampleFromTime(result, out uint sampleTime))
                {
                    MessageBox.Show("Warning: Value was too small. Time is now set to zero seconds. Please increase the value.");
                }
                textBox.Text = GetTimeString(sampleTime);
                textBox.Tag = sampleTime;
            }
            else
            {
                MessageBox.Show("Unable to parse text. Please enter a valid value in milliseconds");
                textBox.Text = "";
                textBox.Tag = null;
            }

            if (groupBoxAnode.Visible && !groupBoxCathode.Visible)
            {
                pulseWidthCathodic.Text = textBox.Text;
                pulseWidthCathodic.Tag = textBox.Tag;
            }
            else if (groupBoxCathode.Visible && !groupBoxAnode.Visible)
            {
                pulseWidthAnodic.Text = textBox.Text;
                pulseWidthAnodic.Tag = textBox.Tag;
            }
        }

        private bool GetSampleFromTime(double value, out uint samples)
        {
            var ratio = value * 1e3 / SamplePeriodMicroSeconds;
            samples = (uint)Math.Round(ratio);

            return !(ratio > uint.MaxValue || ratio < uint.MinValue || samples == 0);
        }

        private bool GetSampleFromAmplitude(double value, out byte samples)
        {
            var ratio = value * GetUnitConversion() / Sequence.CurrentStepSizeuA;
            samples = (byte)Math.Round(ratio);

            return !(ratio > byte.MaxValue || ratio < 0 || samples == 0);
        }

        private double GetTimeFromSample(uint value)
        {
            return value * SamplePeriodMicroSeconds / 1e3;
        }

        private double GetAmplitudeFromSample(byte value)
        {
            return value * Sequence.CurrentStepSizeuA / GetUnitConversion();
        }

        private void Amplitude_TextChanged(object sender, EventArgs e)
        {
            TextBox textBox = (TextBox)sender;

            if (textBox.Text == "" || textBox.Text == "0" || textBox.Text == "0.0" || textBox.Text == "0.00" || textBox.Text == "0.000")
                return;

            if (double.TryParse(textBox.Text, out double result))
            {
                if (!GetSampleFromAmplitude(result, out byte sampleAmplitude))
                {
                    if (sampleAmplitude == 0)
                    {
                        MessageBox.Show("Warning: amplitude is set to zero. Please increase the amplitude value and try again.");
                        textBox.Text = "";
                        textBox.Tag = null;
                        return;
                    }
                    else
                    {
                        MessageBox.Show("Warning: Amplitude is too high for the given step-size. " +
                            "Please increase the amplitude step-size and try again.");
                        sampleAmplitude = byte.MaxValue;
                    }
                }

                textBox.Text = GetAmplitudeString(sampleAmplitude);
                textBox.Tag = sampleAmplitude;
            }
            else
            {
                MessageBox.Show("Unable to parse text. Please enter a valid value in milliamps");
                textBox.Text = "";
                textBox.Tag = null;
            }

            if (groupBoxAnode.Visible && !groupBoxCathode.Visible)
            {
                amplitudeCathodic.Text = textBox.Text;
                amplitudeCathodic.Tag = textBox.Tag;
            }
            else if (groupBoxCathode.Visible && !groupBoxAnode.Visible)
            {
                amplitudeAnodic.Text = textBox.Text;
                amplitudeAnodic.Tag = textBox.Tag;
            }
        }

        private void Checkbox_CheckedChanged(object sender, EventArgs e)
        {
            if (checkboxBiphasicSymmetrical.Checked)
            {
                if (checkBoxAnodicFirst.Checked)
                {
                    groupBoxCathode.Visible = false;
                    groupBoxAnode.Visible = true;
                }
                else
                {
                    groupBoxCathode.Visible = true;
                    groupBoxAnode.Visible = false;
                }
            }
            else
            {
                groupBoxCathode.Visible = true;
                groupBoxAnode.Visible = true;
            }
        }

        private void ButtonClearPulses_Click(object sender, EventArgs e)
        {
            if (ChannelConfiguration.SelectedContacts.All(x => x == false) || ChannelConfiguration.SelectedContacts.All(x => x == true))
            {
                DialogResult result = MessageBox.Show("Caution: All channels are currently selected, and all " +
                    "settings will be cleared if you continue. Press Okay to clear all pulse settings, or Cancel to keep them",
                    "Remove all channel settings?", MessageBoxButtons.OKCancel);

                if (result == DialogResult.Cancel)
                {
                    return;
                }
            }

            for (int i = 0; i < ChannelConfiguration.SelectedContacts.Length; i++)
            {
                if (ChannelConfiguration.SelectedContacts[i])
                {
                    Sequence.Stimuli[i].Clear();
                }
            }

            DrawStimulusWaveform();
        }

        private void ButtonReadPulses_Click(object sender, EventArgs e)
        {
            if (ChannelConfiguration.SelectedContacts.Count(x => x) > 1)
            {
                MessageBox.Show("Too many contacts selected. Please choose a single contact to read from.");
                return;
            }

            int index = -1;

            for (int i = 0; i < ChannelConfiguration.SelectedContacts.Length; i++)
            {
                if (ChannelConfiguration.SelectedContacts[i])
                {
                    index = i; break;
                }
            }

            if (index < 0)
            {
                MessageBox.Show("Warning: No contact selected. Please choose a contact before continuing.");
                return;
            }

            if (Sequence.Stimuli[index].AnodicAmplitudeSteps == Sequence.Stimuli[index].CathodicAmplitudeSteps &&
                Sequence.Stimuli[index].AnodicWidthSamples == Sequence.Stimuli[index].CathodicWidthSamples)
            {
                checkboxBiphasicSymmetrical.Checked = true;
            }
            else
            {
                checkboxBiphasicSymmetrical.Checked = false;
            }

            checkBoxAnodicFirst.Checked = Sequence.Stimuli[index].AnodicFirst;

            Checkbox_CheckedChanged(checkboxBiphasicSymmetrical, e);

            delay.Text = GetTimeString(Sequence.Stimuli[index].DelaySamples); Samples_TextChanged(delay, e);
            amplitudeAnodic.Text = GetAmplitudeString(Sequence.Stimuli[index].AnodicAmplitudeSteps); Amplitude_TextChanged(amplitudeAnodic, e);
            pulseWidthAnodic.Text = GetTimeString(Sequence.Stimuli[index].AnodicWidthSamples); Samples_TextChanged(pulseWidthAnodic, e);
            amplitudeCathodic.Text = GetAmplitudeString(Sequence.Stimuli[index].CathodicAmplitudeSteps); Amplitude_TextChanged(amplitudeCathodic, e);
            pulseWidthCathodic.Text = GetTimeString(Sequence.Stimuli[index].CathodicWidthSamples); Samples_TextChanged(pulseWidthCathodic, e);
            interPulseInterval.Text = GetTimeString(Sequence.Stimuli[index].DwellSamples); Samples_TextChanged(interPulseInterval, e);
            interStimulusInterval.Text = GetTimeString(Sequence.Stimuli[index].InterStimulusIntervalSamples); Samples_TextChanged(interStimulusInterval, e);
            numberOfStimuli.Text = Sequence.Stimuli[index].NumberOfStimuli.ToString();
        }

        private void MenuItemSaveFile_Click(object sender, EventArgs e)
        {
            using SaveFileDialog sfd = new();
            sfd.Filter = "Stimulus Sequence Files (*.json)|*.json";
            sfd.FilterIndex = 1;
            sfd.Title = "Choose where to save the stimulus sequence file";
            sfd.OverwritePrompt = true;
            sfd.ValidateNames = true;

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                DesignHelper.SerializeObject(Sequence, sfd.FileName);
            }
        }

        private void MenuItemLoadFile_Click(object sender, EventArgs e)
        {
            using OpenFileDialog ofd = new();

            ofd.Filter = "Stimulus Sequence Files (*.json)|*.json";
            ofd.FilterIndex = 1;
            ofd.Multiselect = false;
            ofd.Title = "Choose saved stimulus sequence file";

            if (ofd.ShowDialog() == DialogResult.OK)
            {
                if (!File.Exists(ofd.FileName))
                {
                    MessageBox.Show("File does not exist.");
                    return;
                }

                var sequence = DesignHelper.DeserializeString<Rhs2116StimulusSequenceDual>(File.ReadAllText(ofd.FileName));

                if (sequence != null && sequence.Stimuli.Length == 32)
                {
                    Sequence = sequence;
                    dataGridViewStimulusTable.DataSource = Sequence.Stimuli;
                }
                else
                {
                    MessageBox.Show("Incoming sequence is not valid. Check file for validity.");
                }

                DrawStimulusWaveform();
            }
        }
    }
}