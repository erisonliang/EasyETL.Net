﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;

namespace EasyXmlSample
{
    public partial class EasyETLDemoApplication : Form
    {
        private string xmlFileName;
        private string clientCategories = "endpoints;datasources;filestores;messagequeues;transfers;etls";
        public EasyETLDemoApplication()
        {
            InitializeComponent();
            LoadConfiguration();
        }

        private void LoadConfiguration(string visibleNodeFullpath = "")
        {
            tvClients.Nodes.Clear();
            xmlFileName = Path.Combine(Environment.CurrentDirectory, "config.xml");
            if (!File.Exists(xmlFileName))
            {
                using (StreamWriter sw = File.CreateText(xmlFileName))
                {
                    sw.WriteLine("<config><clients></clients><transforms></transforms><profiles></profiles><config>");
                }

            }
            XmlDocument xDoc = new XmlDocument();
            xDoc.Load(xmlFileName);
            AddTreeViewChildNodes(tvClients.Nodes, xDoc.SelectSingleNode("//clients"));
            xDoc = null;
            if (!String.IsNullOrWhiteSpace(visibleNodeFullpath))
            {
                TreeNodeCollection parentNodes = tvClients.Nodes;
                TreeNode tn = null;
                foreach (string nodePath in visibleNodeFullpath.Split('\\'))
                {
                    if (tn != null) parentNodes = tn.Nodes;
                    foreach (TreeNode subtreeNode in parentNodes)
                    {
                        if (subtreeNode.Text.Equals(nodePath)) tn = subtreeNode;
                    }
                }
                if (tn != null)
                {
                    tn.EnsureVisible();
                    tvClients.SelectedNode = tn;
                }
            }
        }

        // Add the children of this XML node 
        // to this child nodes collection.
        private void AddTreeViewChildNodes(
            TreeNodeCollection parent_nodes, XmlNode xml_node, int currentDepth = 0)
        {
            foreach (XmlNode child_node in xml_node.ChildNodes)
            {
                // Make the new TreeView node.
                string clientid = child_node.Name;
                string clientname = child_node.Name;
                if ((child_node.Attributes != null) && (child_node.Attributes["id"] != null))
                {
                    clientid = child_node.Attributes["id"].Value;
                    clientname = child_node.Attributes["name"].Value;
                }
                else
                {
                    if (child_node.ParentNode.Name == "client")
                    {
                        clientid = child_node.Name + "_" + child_node.ParentNode.Attributes["id"].Value;
                    }
                    clientname = child_node.Name;
                }
                TreeNode new_node = parent_nodes.Add(clientid, clientname);

                // Recursively make this node's descendants.
                if (currentDepth < 2) AddTreeViewChildNodes(new_node.Nodes, child_node, currentDepth + 1);

                // If this is a leaf node, make sure it's visible.
                //if (new_node.Nodes.Count == 0) new_node.EnsureVisible();
            }
        }

        private void ShowNewForm(object sender, EventArgs e)
        {
            TreeNode tNode = null;
            if ((tvClients.SelectedNode == null) || (tvClients.SelectedNode.Parent == null))
            {
                int nodeCount = tvClients.Nodes.Count + 1;
                while (tvClients.Nodes[nodeCount.ToString()] != null) nodeCount++;
                tNode = tvClients.Nodes.Add(nodeCount.ToString(), "Client" + nodeCount.ToString());
            }
            else
            {
                TreeNode categoryNode = tvClients.SelectedNode;
                if (clientCategories.Split(';').Contains(tvClients.SelectedNode.Parent.Text)) categoryNode = tvClients.SelectedNode.Parent;
                if (tNode == null) tNode = tvClients.SelectedNode.Nodes.Add(categoryNode.Parent.Name + "_" + categoryNode.Text.TrimEnd('s') + "_" + (categoryNode.Nodes.Count + 1).ToString());
            }
            if (tNode != null)
            {
                SaveXmlFile(tNode, tNode.Text);
                LoadConfiguration(tNode.FullPath);
            }
        }

        private void OpenFile(object sender, EventArgs e)
        {
            tvClients_DoubleClick(this, null);
        }

        private void SaveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            saveFileDialog.Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*";
            if (saveFileDialog.ShowDialog(this) == DialogResult.OK)
            {
                string FileName = saveFileDialog.FileName;
            }
        }

        private void ExitToolsStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void CutToolStripMenuItem_Click(object sender, EventArgs e)
        {
        }

        private void CopyToolStripMenuItem_Click(object sender, EventArgs e)
        {
        }

        private void PasteToolStripMenuItem_Click(object sender, EventArgs e)
        {
        }

        private void ToolBarToolStripMenuItem_Click(object sender, EventArgs e)
        {
            toolStrip.Visible = toolBarToolStripMenuItem.Checked;
        }

        private void StatusBarToolStripMenuItem_Click(object sender, EventArgs e)
        {
            statusStrip.Visible = statusBarToolStripMenuItem.Checked;
        }

        private void CascadeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            LayoutMdi(MdiLayout.Cascade);
        }

        private void TileVerticalToolStripMenuItem_Click(object sender, EventArgs e)
        {
            LayoutMdi(MdiLayout.TileVertical);
        }

        private void TileHorizontalToolStripMenuItem_Click(object sender, EventArgs e)
        {
            LayoutMdi(MdiLayout.TileHorizontal);
        }

        private void ArrangeIconsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            LayoutMdi(MdiLayout.ArrangeIcons);
        }

        private void CloseAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (Form childForm in MdiChildren)
            {
                childForm.Close();
            }
        }

        private void tvClients_AfterLabelEdit(object sender, NodeLabelEditEventArgs e)
        {
            if (e.CancelEdit) return;
            if (String.IsNullOrWhiteSpace(e.Label)) return;
            if (e.Node.Text != e.Label)
            {
                SaveXmlFile(e.Node, e.Label);
            }
        }

        private void SaveXmlFile(TreeNode node, string newLabel)
        {
            XmlDocument xDoc = new XmlDocument();
            xDoc.Load(xmlFileName);
            XmlNode xNode = xDoc.SelectSingleNode("//clients/client[@id='" + node.Name + "']");
            if (xNode != null)
            {
                xNode.Attributes["name"].Value = newLabel;
            }
            else
            {
                if (node.FullPath.Contains('\\'))
                {
                    string clientName = node.FullPath.Split('\\')[0];
                    string nodeCategory = node.FullPath.Split('\\')[1];
                    string nodeName = node.FullPath.Split('\\')[2];
                    string nodeLabel = newLabel;
                    xNode = xDoc.SelectSingleNode("//clients/client[@name='" + clientName + "']/" + nodeCategory + "/" + nodeCategory.TrimEnd('s') + "[@id='" + node.Name + "']");
                    if (xNode != null)
                    {
                        xNode.Attributes["name"].Value = newLabel;
                    }
                    else
                    {
                        xNode = xDoc.SelectSingleNode("//clients/client[@name='" + clientName + "']/" + nodeCategory);
                        XmlElement xElement = xDoc.CreateElement(nodeCategory.TrimEnd('s'));
                        xElement.SetAttribute("id", nodeName);
                        xElement.SetAttribute("name", nodeLabel);
                        xNode.AppendChild(xElement);
                    }
                }
                else
                {
                    string nodeType = "client";
                    XmlElement childNode = xDoc.CreateElement(nodeType);
                    childNode.SetAttribute("id", node.Name);
                    childNode.SetAttribute("name", node.Text);
                    foreach (string clientCategory in clientCategories.Split(';'))
                    {
                        XmlElement ccNode = xDoc.CreateElement(clientCategory);
                        childNode.AppendChild(ccNode);
                    }

                    xDoc.SelectSingleNode("//clients").AppendChild(childNode);
                }
            }
            xDoc.Save(xmlFileName);
        }

        private void tvClients_BeforeLabelEdit(object sender, NodeLabelEditEventArgs e)
        {
            TreeNode selectedNode = e.Node;
            e.CancelEdit = !CanAllowEdit(selectedNode);
        }

        private bool CanAllowEdit(TreeNode selectedNode)
        {
            bool allowEdit = false;
            if (selectedNode.Parent == null) allowEdit = true;
            if ((!allowEdit) && (selectedNode.Nodes.Count == 0) && (selectedNode.Parent !=null) && (selectedNode.Parent.Parent !=null)) allowEdit = true;
            if (allowEdit)
            {
                foreach (TabPage p in MainTablControl.TabPages)
                {
                    if (p.Text == tvClients.SelectedNode.FullPath)
                    {
                        MessageBox.Show("This configuration is open.  Please close window before attempting to rename.");
                        allowEdit = false;
                        break;
                    }
                }
            }
            return allowEdit;
        }

        private void tvClients_DoubleClick(object sender, EventArgs e)
        {

            if ((tvClients.SelectedNode != null) && (tvClients.SelectedNode.Parent != null) && (tvClients.SelectedNode.Parent.Text == "etls") && (tvClients.SelectedNode.Parent.Parent != null))
            {
                foreach (TabPage p in MainTablControl.TabPages)
                {
                    if (p.Text == tvClients.SelectedNode.FullPath)
                    {
                        MainTablControl.SelectedTab = p;
                        return;
                    }
                }
                MainTablControl.TabPages.Add(tvClients.SelectedNode.FullPath);
                TabPage newTabPage = MainTablControl.TabPages[MainTablControl.TabCount - 1];
                ETLForm mForm = new ETLForm();
                mForm.TopLevel = false;
                mForm.MdiParent = this;
                mForm.FormBorderStyle = FormBorderStyle.None;
                mForm.SettingsFileName = xmlFileName;
                mForm.LoadControls();
                mForm.LoadSettingsFromXml(tvClients.SelectedNode.FullPath);
                newTabPage.Controls.Add(mForm);
                mForm.Dock = DockStyle.Fill;
                mForm.Show();
                //mForm.LoadDataToGridView();
                MainTablControl.SelectedTab = newTabPage;
            }
        }

        private void saveToolStripButton_Click(object sender, EventArgs e)
        {
            if (MainTablControl.SelectedTab != null)
            {
                ((ETLForm)MainTablControl.SelectedTab.Controls[0]).SaveSettingsToXmlFile();
            }
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            saveToolStripButton_Click(this, null);
        }

        private void tvClients_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.Delete)
            {
                
            }
        }

        private void DeleteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            bool allowDelete = (tvClients.SelectedNode != null) && ((tvClients.SelectedNode.Parent == null) || (tvClients.SelectedNode.Nodes.Count == 0));
            TreeNode node = tvClients.SelectedNode;
            if (allowDelete)
            {
                foreach (TabPage p in MainTablControl.TabPages)
                {
                    if (p.Text == node.FullPath)
                    {
                        MessageBox.Show("This configuration is open.  Please close window before attempting to delete the node.");
                        allowDelete = false;
                        break;
                    }
                }
            }

            if ((allowDelete) && (MessageBox.Show("Deleted nodes cannot be recovered.  Are you sure to delete ?", "Deleting Configuration", MessageBoxButtons.YesNo) == DialogResult.Yes))
            {
                XmlDocument xDoc = new XmlDocument();
                xDoc.Load(xmlFileName);
                string xPath = "//clients/client[@name='" + node.FullPath + "']";
                if (node.FullPath.Contains('\\'))
                {
                    string clientName = node.FullPath.Split('\\')[0];
                    string nodeCategory = node.FullPath.Split('\\')[1];
                    string nodeName = node.FullPath.Split('\\')[2];
                    xPath = "//clients/client[@name='" + clientName + "']/" + nodeCategory + "/" + nodeCategory.TrimEnd('s') + "[@id='" + node.Name + "']";
                }
                XmlNode xNode = xDoc.SelectSingleNode(xPath);
                xNode.ParentNode.RemoveChild(xNode);
                xDoc.Save(xmlFileName);
                LoadConfiguration();
            }
        }

        private void renameStripMenuItem_Click(object sender, EventArgs e)
        {
            if ((tvClients.SelectedNode != null) && (CanAllowEdit(tvClients.SelectedNode))) tvClients.SelectedNode.BeginEdit();
        }

        private void RenameToolStripButton_Click(object sender, EventArgs e)
        {
            renameStripMenuItem_Click(this, null);
        }

        private void DeleteToolStripButton_Click(object sender, EventArgs e)
        {
            DeleteToolStripMenuItem_Click(this, null);
        }

        private void CloseToolStripButton_Click(object sender, EventArgs e)
        {
            CloseToolStripMenuItem_Click(this, null);
        }

        private void CloseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (MainTablControl.SelectedTab != null) MainTablControl.TabPages.Remove(MainTablControl.SelectedTab);
        }

        private void ExitToolStripButton_Click(object sender, EventArgs e)
        {
            ExitToolsStripMenuItem_Click(this, null);
        }

        private void MoveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if ((tvClients.SelectedNode !=null) && (tvClients.SelectedNode.FullPath.Split('\\').Length == 3) && CanAllowEdit(tvClients.SelectedNode))
            {
                MoveConfigurationForm moveForm = new MoveConfigurationForm();
                moveForm.OriginalPath = tvClients.SelectedNode.FullPath;
                moveForm.SettingsFileName = xmlFileName;
                moveForm.LoadClients(tvClients.SelectedNode.FullPath.Split('\\')[0]);
                moveForm.SetMoveLabel("Move " + tvClients.SelectedNode.Text + " to:");
                moveForm.Text = "Move from " + tvClients.SelectedNode.FullPath.Split('\\')[0];
                moveForm.ShowDialog(this);
                LoadConfiguration();
                //MessageBox.Show("We can move this");
            }
        }

        private void MoveToolStripButton_Click(object sender, EventArgs e)
        {
            MoveToolStripMenuItem_Click(this, null);
        }

        private void CloneToolStripButton_Click(object sender, EventArgs e)
        {
            CloneToolStripMenuItem_Click(this, null);
        }

        private void CloneToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (tvClients.SelectedNode != null)
            {
                XmlDocument xDoc = new XmlDocument();
                xDoc.Load(xmlFileName);
                TreeNode node = tvClients.SelectedNode;
                string xPath = "//clients/client[@name='" + node.FullPath + "']";
                if (node.FullPath.Contains('\\'))
                {
                    string clientName = node.FullPath.Split('\\')[0];
                    string nodeCategory = node.FullPath.Split('\\')[1];
                    string nodeName = node.FullPath.Split('\\')[2];
                    xPath = "//clients/client[@name='" + clientName + "']/" + nodeCategory + "/" + nodeCategory.TrimEnd('s') + "[@name='" + node.Text + "']";
                }
                XmlNode xNode = xDoc.SelectSingleNode(xPath);
                XmlNode cloneNode = xNode.CloneNode(true);
                cloneNode.Attributes.GetNamedItem("name").Value = "Copy of " + node.Text;
                xNode.ParentNode.AppendChild(cloneNode);
                xDoc.Save(xmlFileName);
                LoadConfiguration();
            }
        }



    }
}
