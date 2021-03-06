﻿using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Metadata;
using MsCrmTools.FlsBulkUpdater.AppCode;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using XrmToolBox.Extensibility;
using XrmToolBox.Extensibility.Interfaces;

namespace MsCrmTools.FlsBulkUpdater
{
    public partial class MainControl : PluginControlBase, IGitHubPlugin, IHelpPlugin
    {
        private bool useIcons = true;
        private List<SecureFieldInfo> fields;
        private List<Entity> profiles;
        private EntityMetadataCollection metadata;

        public MainControl()
        {
            InitializeComponent();

            CbbRead.SelectedIndex = 0;
            CbbCreate.SelectedIndex = 0;
            CbbUpdate.SelectedIndex = 0;
        }

        public void LoadFls()
        {
            WorkAsync(new WorkAsyncInfo
            {
                Message = "Loading Field Security profiles...",
                Work = (bw, e) =>
                {
                    var flsManager = new FlsManager(Service);
                    profiles = flsManager.LoadSecureProfiles();

                    bw.ReportProgress(0, "Loading Secured fields...");
                    fields = flsManager.LoadSecureFields();

                    var dico = new Dictionary<string, List<string>>();

                    foreach (var field in fields)
                    {
                        if (!dico.ContainsKey(field.Entity))
                        {
                            dico.Add(field.Entity, new List<string>());
                        }

                        if (!dico[field.Entity].Contains(field.Attribute))
                        {
                            dico[field.Entity].Add(field.Attribute);
                        }
                    }

                    bw.ReportProgress(0, "Loading metadata...");
                    metadata = MetadataHelper.LoadMetadata(dico, Service);

                    foreach (var field in fields)
                    {
                        field.AttributeDisplayName =
                            metadata.First(m => m.LogicalName == field.Entity)
                                .Attributes.First(a => a.LogicalName == field.Attribute)
                                .DisplayName?.UserLocalizedLabel.Label ?? "N/A";
                    }
                },
                PostWorkCallBack = e =>
                {
                    DisplaySecuredAttributes();

                    var profilesList = new List<ListViewItem>();

                    foreach (var profile in profiles)
                    {
                        var item = new ListViewItem(profile.GetAttributeValue<string>("name")) { Tag = profile };
                        profilesList.Add(item);
                    }

                    lvFlsRoles.Items.Clear();
                    lvFlsRoles.Items.AddRange(profilesList.ToArray());
                },
                ProgressChanged = e => { SetWorkingMessage(e.UserState.ToString()); }
            });
        }

        private void DisplaySecuredAttributes(object filter = null)
        {
            string sFilter = filter?.ToString().ToLower();
            var action = new MethodInvoker(delegate
            {
                var fieldsList = new List<ListViewItem>();

                foreach (var field in fields.Where(f => sFilter == null
                || f.Attribute.ToLower().Contains(sFilter)
                || f.AttributeDisplayName.ToLower().Contains(sFilter)
                ))
                {
                    var entityDislayName = metadata.First(m => m.LogicalName == field.Entity).DisplayName.UserLocalizedLabel.Label;
                    var attributeDisplayName = metadata.First(m => m.LogicalName == field.Entity)
                            .Attributes.First(a => a.LogicalName == field.Attribute)
                            .DisplayName.UserLocalizedLabel.Label;

                    var item = new ListViewItem(attributeDisplayName);
                    item.SubItems.Add(field.Attribute);
                    item.SubItems.Add(entityDislayName);
                    item.SubItems.Add(field.Entity);
                    item.SubItems.Add(string.Empty);
                    item.SubItems.Add(string.Empty);
                    item.SubItems.Add(string.Empty);
                    fieldsList.Add(item);
                }


                LvSecuredAttributes.Items.Clear();
                LvSecuredAttributes.Items.AddRange(fieldsList.ToArray());
                lvFlsRoles_SelectedIndexChanged(null, null);
            });

            if (LvSecuredAttributes.InvokeRequired)
            {
                LvSecuredAttributes.Invoke(action);
            }
            else
            {
                action();
            }
        }

        private void ListView_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            var lv = (ListView)sender;

            lv.Sorting = lv.Sorting == SortOrder.Ascending ? SortOrder.Descending : SortOrder.Ascending;

            lv.ListViewItemSorter = new ListViewItemComparer(e.Column, lv.Sorting);
        }

        private void llSecureAttrCheckAll_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            var lv = ((LinkLabel)sender) == llSecureAttrCheckAll ? LvSecuredAttributes : lvFlsRoles;
            if (lv.Items.Count == 0) return;

            if (((LinkLabel)sender).Text == "Select all")
            {
                foreach (ListViewItem item in lv.Items)
                {
                    item.Checked = true;
                }

                ((LinkLabel)sender).Text = "Select none";
            }
            else
            {
                foreach (ListViewItem item in lv.Items)
                {
                    item.Checked = false;
                }

                ((LinkLabel)sender).Text = "Select all";
            }
        }

        private void lvFlsRoles_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lvFlsRoles.SelectedItems.Count == 0)
                return;

            var profileId = ((Entity)lvFlsRoles.SelectedItems[0].Tag).Id;

            foreach (ListViewItem item in LvSecuredAttributes.Items)
            {
                var attribute = item.SubItems[1].Text;
                var entity = item.SubItems[3].Text;

                var secureField = fields.First(f => f.Entity == entity && f.Attribute == attribute);
                var secureFieldForProfile = secureField.Fields.FirstOrDefault(
                        f => f.GetAttributeValue<EntityReference>("fieldsecurityprofileid").Id == profileId);

                if (secureFieldForProfile != null)
                {
                    item.SubItems[4].Text = (secureFieldForProfile.GetAttributeValue<OptionSetValue>("canread").Value == 4).ToString();
                    item.SubItems[5].Text = (secureFieldForProfile.GetAttributeValue<OptionSetValue>("cancreate").Value == 4).ToString();
                    item.SubItems[6].Text = (secureFieldForProfile.GetAttributeValue<OptionSetValue>("canupdate").Value == 4).ToString();
                }
                else
                {
                    item.SubItems[4].Text = false.ToString();
                    item.SubItems[5].Text = false.ToString();
                    item.SubItems[6].Text = false.ToString();
                }
            }
        }

        private void tsbClose_Click(object sender, EventArgs e)
        {
            CloseTool();
        }

        private void TsbLoadFls_Click(object sender, EventArgs e)
        {
            ExecuteMethod(LoadFls);
        }

        private void tsbUpdate_Click(object sender, EventArgs e)
        {
            var us = new UpdateSettings
            {
                Profiles = lvFlsRoles.CheckedItems.Cast<ListViewItem>().Select(l => (Entity)l.Tag).ToList(),
                Fields = new List<SecureFieldInfo>()
            };

            foreach (ListViewItem item in LvSecuredAttributes.Items)
            {
                if (item.Checked)
                {
                    var attribute = item.SubItems[1].Text;
                    var entity = item.SubItems[3].Text;

                    us.Fields.Add(fields.First(f => f.Entity == entity && f.Attribute == attribute));
                }
            }

            if (CbbCreate.SelectedIndex != 0)
            {
                us.CanCreate = CbbCreate.SelectedIndex == 2;
            }

            if (CbbRead.SelectedIndex != 0)
            {
                us.CanRead = CbbRead.SelectedIndex == 2;
            }

            if (CbbUpdate.SelectedIndex != 0)
            {
                us.CanUpdate = CbbUpdate.SelectedIndex == 2;
            }

            WorkAsync(new WorkAsyncInfo
            {
                Message = "Updating secure fields...",
                AsyncArgument = us,
                Work = (bw, evt) =>
                {
                    var uSettings = (UpdateSettings)evt.Argument;

                    foreach (var field in uSettings.Fields)
                    {
                        field.CanCreate = null;
                        field.CanRead = null;
                        field.CanUpdate = null;

                        if (us.CanCreate.HasValue)
                        {
                            field.CanCreate = us.CanCreate.Value;
                        }

                        if (us.CanRead.HasValue)
                        {
                            field.CanRead = us.CanRead.Value;
                        }

                        if (us.CanUpdate.HasValue)
                        {
                            field.CanUpdate = us.CanUpdate.Value;
                        }

                        field.Update(Service, uSettings.Profiles);
                    }
                },
                PostWorkCallBack = evt =>
                {
                    if (evt.Error != null)
                    {
                        MessageBox.Show(this, "An error occured: " + evt.Error.Message, "Error", MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }

                    lvFlsRoles_SelectedIndexChanged(null, null);

                   LvSecuredAttributes.Invalidate();
                }
            });
        }
        
        private Thread searchThread;
        private void tstSearch_TextChanged(object sender, EventArgs e)
        {
            if (searchThread != null)
            {
                searchThread.Abort();
            }

            searchThread = new Thread(DisplaySecuredAttributes);
            searchThread.Start(tstSearch.Text);
        }

        private void listView_DrawSubItem(object sender, DrawListViewSubItemEventArgs e)
        {
            if (!useIcons || string.IsNullOrEmpty(e.SubItem.Text))
            {
                e.DrawDefault = true;
                return;
            }

            if (e.ColumnIndex < 4 || e.ColumnIndex > 6)
            {
                e.DrawDefault = true;
                return;
            }

            e.DrawBackground();
            var imageRect = new Rectangle(e.Bounds.X + (e.Bounds.Width - e.Bounds.Height)/2, e.Bounds.Y, e.Bounds.Height, e.Bounds.Height);
            e.Graphics.DrawImage(iiIcons.Images[e.SubItem.Text.ToLower() == "false" ? 0 : 1], imageRect);
        }

        private void listView_DrawColumnHeader(object sender, DrawListViewColumnHeaderEventArgs e)
        {
            e.DrawDefault = true;
        }

        private void booleanValueToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (useIcons)
            {
                if (searchThread != null)
                {
                    searchThread.Abort();
                }

                searchThread = new Thread(DisplaySecuredAttributes);
                searchThread.Start(tstSearch.Text);
            }

            useIcons = false;
            tssbDisplayStyle.Text = string.Format(tssbDisplayStyle.Tag.ToString(), "Boolean values");
        }

        private void iconToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!useIcons)
            {
                if (searchThread != null)
                {
                    searchThread.Abort();
                }

                searchThread = new Thread(DisplaySecuredAttributes);
                searchThread.Start(tstSearch.Text);
            }

            useIcons = true;
            tssbDisplayStyle.Text = string.Format(tssbDisplayStyle.Tag.ToString(), "Icons");
        }

        public string RepositoryName => "MsCrmTools.FlsBulkUpdater";
        public string UserName => "MscrmTools";
        public string HelpUrl => "https://github.com/MscrmTools/MsCrmTools.FlsBulkUpdater/wiki";
    }
}