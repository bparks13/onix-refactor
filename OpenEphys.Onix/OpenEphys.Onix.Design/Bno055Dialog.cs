using System;

namespace OpenEphys.Onix.Design
{
    public partial class Bno055Dialog : GenericDeviceDialog
    {
        public ConfigureBno055 ConfigureBno055
        {
            get => (ConfigureBno055)propertyGrid.SelectedObject;
            set => propertyGrid.SelectedObject = value;
        }

        public Bno055Dialog(ConfigureBno055 configureNode)
        {
            InitializeComponent();
            Shown += FormShown;

            ConfigureBno055 = new(configureNode);
        }

        private void FormShown(object sender, EventArgs e)
        {
            if (!TopLevel)
            {
                splitContainer1.Panel2Collapsed = true;
                splitContainer1.Panel2.Hide();

                MaximumSize = new System.Drawing.Size(0, 0);
            }
        }
    }
}
