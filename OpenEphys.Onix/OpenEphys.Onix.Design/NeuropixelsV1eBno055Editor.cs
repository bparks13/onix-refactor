﻿using System;
using System.ComponentModel;
using Bonsai.Design;
using System.Windows.Forms;

namespace OpenEphys.Onix.Design
{
    internal class NeuropixelsV1eBno055Editor : WorkflowComponentEditor
    {
        public override bool EditComponent(ITypeDescriptorContext context, object component, IServiceProvider provider, IWin32Window owner)
        {
            if (provider != null)
            {
                var editorState = (IWorkflowEditorState)provider.GetService(typeof(IWorkflowEditorState));
                if (editorState != null && !editorState.WorkflowRunning && component is ConfigureNeuropixelsV1eBno055 configureBno055)
                {
                    using var editorDialog = new NeuropixelsV1eBno055Dialog(configureBno055);

                    if (editorDialog.ShowDialog() == DialogResult.OK)
                    {
                        configureBno055.Enable = editorDialog.ConfigureNode.Enable;

                        return true;
                    }
                }
            }

            return false;
        }
    }
}