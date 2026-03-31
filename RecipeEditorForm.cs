using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Forms;
using MetroFramework.Forms;
using Microsoft.WindowsAPICodePack.Dialogs;
using Newtonsoft.Json.Linq;
using Button = System.Windows.Forms.Button;
using Panel = System.Windows.Forms.Panel;
using TextBox = System.Windows.Forms.TextBox;

namespace AMI_Manager.Forms.Main
{
    public partial class RecipeEditorForm : MetroForm
    {

        ManagerForm managerForm = null;
        private System.Windows.Forms.Panel titleBar;


        enum json_type
        {
            Obejct,
            Array,
            Value,
        }
        enum Get_node_mode
        {
            Inform,
            Delete,
        }

        enum Path_skip_mode
        {
            Skip,
            Noskip,
        }

        enum Load_json_mode
        {
            Prev,
            Current,
        }


        static string settingsFilePath = global::AMI_Manager.Properties.Resources.settingsFilePath;
        static string settingsFolderPath = global::AMI_Manager.Properties.Resources.settingFolderPath;
        static readonly Encoding settingsEncoding = new UTF8Encoding(false);


        //string settingsFilePath = @"settings.txt";
        private string jsonFilePath { get; set; }
        private JObject jsonObject { get; set; }
        private TreeNode mySelectedNode { get; set; }
        private string BeforeJsonText { get; set; }
        string JsonfolderPath { get; set; }



        private List<string> searchResults = new List<string>();
        private int currentIndex = -1;

        private System.Windows.Forms.TreeNode NodeSource;
        private System.Windows.Forms.TreeNode NodeTarget;
        private bool isF2EditRequested = false;
        private const int NodePreviewMaxLength = 120;

        private const int GWL_STYLE = -16;
        private const int WS_HSCROLL = 0x00100000;
        private const int TVS_NOHSCROLL = 0x8000;
        private const int SB_HORZ = 0;
        private static readonly IntPtr HWND_TOP = IntPtr.Zero;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_FRAMECHANGED = 0x0020;
        private const int TV_FIRST = 0x1100;
        private const int TVM_GETEDITCONTROL = TV_FIRST + 15;

        [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
        private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool ShowScrollBar(IntPtr hWnd, int wBar, bool bShow);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        public RecipeEditorForm(ManagerForm _managerForm)
        {
            InitializeComponent();
            managerForm = _managerForm;

            CreateCustomTitleBar();

            treeViewJson.NodeMouseClick += TreeView_NodeMouseClick;
            treeViewJson.AfterSelect += TreeViewJson_AfterSelect_ShowFullText;
            objectToolStripMenuItem.Click += objectToolStripMenuItem_Click;
            valueToolStripMenuItem.Click += valueToolStripMenuItem_Click;
            arrayToolStripMenuItem.Click += arrayToolStripMenuItem_Click;
            DeleteNodeToolStripMenuItem.Click += deleteNodeToolStripMenuItem_Click;
            copyNodeToolStripMenuItem.Click += copyNodeToolStripMenuItem_Click;
            pasteNodeToolStripMenuItem.Click += pasteToolStripMenuItem_Click;


            treeViewJson.AfterLabelEdit += new NodeLabelEditEventHandler(treeViewJson_AfterLabelEdit);
            treeViewJson.BeforeLabelEdit += treeViewJson_BeforeLabelEdit;
            treeViewJson.GetType().GetProperty("DoubleBuffered", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(treeViewJson, true);
            treeViewJson.Scrollable = true;
            treeViewJson.ShowNodeToolTips = true;
            treeViewJson.DrawMode = TreeViewDrawMode.Normal;
            treeViewJson.HandleCreated += (s, e) => EnableTreeViewHorizontalScrollBar();
            treeViewJson.Resize += (s, e) => EnableTreeViewHorizontalScrollBar();
            AdjustTreeViewLayoutWidth();

            LoadPreviousFilePath();
            LoadPreviousFolderPath();
        }

        private void EnableTreeViewHorizontalScrollBar()
        {
            int style = GetWindowLong32(treeViewJson.Handle, GWL_STYLE);
            style |= WS_HSCROLL;
            style &= ~TVS_NOHSCROLL;
            SetWindowLong32(treeViewJson.Handle, GWL_STYLE, style);
            SetWindowPos(treeViewJson.Handle, HWND_TOP, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);
            ShowScrollBar(treeViewJson.Handle, SB_HORZ, true);
        }

        private void AdjustTreeViewLayoutWidth()
        {
            if (tableLayoutPanel1 == null || tableLayoutPanel1.ColumnStyles.Count < 3)
                return;

            if (tableLayoutPanel1.ColumnStyles[1].SizeType == SizeType.Percent &&
                tableLayoutPanel1.ColumnStyles[2].SizeType == SizeType.Percent)
            {
                tableLayoutPanel1.ColumnStyles[1].Width = 45F; // TreeView 영역 확장
                tableLayoutPanel1.ColumnStyles[2].Width = 22F; // Text 영역 축소
            }
        }

        private void LoadPreviousFilePath()
        {
            string previousFilePath = ReadSettingPath(settingsFilePath);
            if (!string.IsNullOrWhiteSpace(previousFilePath))
            {
                jsonFilePath = previousFilePath;
                LoadJson(jsonFilePath);
                PopulateTreeView();
            }
            else
            {
                //MessageBox.Show(this, "마지막 열었던 파일이 존재하지 않습니다!", "WARNING");
            }
        }

        private void LoadPreviousFolderPath()
        {
            string previousFolderPath = ReadSettingPath(settingsFolderPath);
            if (!string.IsNullOrWhiteSpace(previousFolderPath))
            {
                string FolderPath = previousFolderPath;
                LoadFilesToDataGridView(FolderPath);
            }
            else
            {
                //MessageBox.Show(this, "마지막 열었던 폴더가 존재하지 않습니다!", "WARNING");
            }
        }




        private void LoadJson(string json_path)
        {
            try
            {
                if (File.Exists(json_path))
                {
                    string jsonText = File.ReadAllText(json_path);
                    jsonObject = JObject.Parse(jsonText);
                    richTextBox_json.Text = jsonText;
                    BeforeJsonText = jsonText;
                }
                else
                {
                    jsonObject = new JObject();
                    BeforeJsonText = jsonObject.ToString();
                }
            }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException || ex is Newtonsoft.Json.JsonException)
            {
                MessageBox.Show(ex.Message, "WARNING");
            }
        }

        private void PopulateTreeView()
        {
            treeViewJson.Nodes.Clear();
            TreeNode rootNode = new TreeNode("Root");
            AddNodes(jsonObject, rootNode);
            UpdateNodeToolTips(rootNode);
            treeViewJson.Nodes.Add(rootNode);
            EnableTreeViewHorizontalScrollBar();
            //treeView1.ExpandAll();

        }

        private void AddNodes(JToken token, TreeNode node)
        {
            if (token is JObject obj)
            {
                foreach (var property in obj.Properties())
                {
                    TreeNode childNode;
                    if (property.Value is JValue value)
                    {
                        string fullNodeText = $"{property.Name}:{property.Value}";
                        string nodeText = ToNodePreview(fullNodeText);
                        childNode = new TreeNode(nodeText);
                        childNode.Tag = fullNodeText;
                        string value_type = property.Value.Type.ToString();

                        if (value_type == "Float" || value_type == "Integer")
                        {
                            childNode.ImageIndex = 3;
                            childNode.SelectedImageIndex = 3;
                        }
                        else if (value_type == "Boolean")
                        {
                            childNode.ImageIndex = 4;
                            childNode.SelectedImageIndex = 4;
                        }
                        else if (value_type == "String")
                        {
                            childNode.ImageIndex = 5;
                            childNode.SelectedImageIndex = 5;
                        }
                        else
                        {
                            childNode.ImageIndex = 1;
                            childNode.SelectedImageIndex = 1;
                        }

                        node.Nodes.Add(childNode);
                        AddNodes(property.Value, childNode);

                    }
                    else
                    {
                        childNode = new TreeNode(property.Name);
                        childNode.Tag = childNode.Text;
                        if (property.Value.Type == JTokenType.Array)
                        {
                            //childNode.Text = $"{property.Name}[{property.Value.Count()}]";
                            childNode.Text = $"{property.Name}";
                            childNode.ImageIndex = 2;
                            childNode.SelectedImageIndex = 2;
                        }
                        else
                        {
                            //childNode.Text = $"{property.Name} ({property.Value.Count()})";
                            childNode.Text = $"{property.Name}";
                            childNode.ImageIndex = 0;
                            childNode.SelectedImageIndex = 0;
                        }

                        node.Nodes.Add(childNode);
                        AddNodes(property.Value, childNode);
                    }
                }
            }
            else if (token is JArray array)
            {
                for (int i = 0; i < array.Count; i++)
                {
                    TreeNode childNode;
                    JToken item = array[i];
                    if (item is JObject)
                    {
                        //childNode = new TreeNode($"[{i}] ({item.Count()})");
                        childNode = new TreeNode($"[{i}]");
                        childNode.Tag = childNode.Text;
                        childNode.ImageIndex = 0;
                        childNode.SelectedImageIndex = 0;
                    }
                    else if (item is JArray)
                    {
                        //childNode = new TreeNode($"[{i}] ({item.Count()})");
                        childNode = new TreeNode($"[{i}]");
                        childNode.Tag = childNode.Text;
                        childNode.ImageIndex = 2;
                        childNode.SelectedImageIndex = 2;
                    }
                    else
                    {
                        childNode = new TreeNode($"[{i}]:Value");
                        childNode.Tag = childNode.Text;
                        childNode.ImageIndex = 1;
                        childNode.SelectedImageIndex = 1;
                    }

                    node.Nodes.Add(childNode);
                    AddNodes(item, childNode);
                }
            }
        }

        private void ExpandAllNodes(TreeNodeCollection nodes)
        {
            foreach (TreeNode node in nodes)
            {
                node.Expand();
                ExpandAllNodes(node.Nodes);
            }
        }

        private TreeNode FindNode(TreeNodeCollection nodes, string searchText)
        {
            foreach (TreeNode node in nodes)
            {
                if (node.Text.ToLower().Contains(searchText))
                {
                    return node;
                }
                TreeNode foundNode = FindNode(node.Nodes, searchText);
                if (foundNode != null)
                {
                    return foundNode;
                }
            }
            return null;
        }

        private TreeNode FindNextNode(TreeNode startNode, string searchText)
        {
            TreeNode currentNode = startNode;
            ExpandAllNodes(treeViewJson.Nodes);
            while (currentNode != null)
            {
                if (currentNode.Text.ToLower().Contains(searchText))
                {
                    return currentNode;
                }
                currentNode = currentNode.NextVisibleNode;
            }
            return null;
        }

        private TreeNode FindPrevNode(TreeNode startNode, string searchText)
        {
            TreeNode currentNode = startNode;
            ExpandAllNodes(treeViewJson.Nodes);
            while (currentNode != null)
            {
                if (currentNode.Text.ToLower().Contains(searchText))
                {
                    return currentNode;
                }
                currentNode = currentNode.PrevVisibleNode;
            }
            return null;
        }

        private void TreeViewJson_MouseMove(object sender, MouseEventArgs e)
        {
            TreeNode node = treeViewJson.GetNodeAt(e.X, e.Y);
            if (node != null)
            {
                rtbNodeLocation.Text = BuildNodeDetailText(node);
            }
        }

        private void TreeViewJson_AfterSelect_ShowFullText(object sender, TreeViewEventArgs e)
        {
            if (e.Node == null)
                return;

            rtbNodeLocation.Text = BuildNodeDetailText(e.Node);
            ScrollRichTextToNode(e.Node);
        }

        private string BuildNodeDetailText(TreeNode node)
        {
            string nodePath = GetNodePath(node, Get_node_mode.Inform);
            string fullNodeText = GetFullNodeText(node);

            try
            {
                string selectPath = Select_json_path(node, Path_skip_mode.Skip).Replace("/", ".");
                if (selectPath.Contains(":"))
                {
                    selectPath = selectPath.Substring(0, selectPath.LastIndexOf(':'));
                }

                JToken selectToken = selectPath == "Root" ? jsonObject.SelectToken("$") : jsonObject.SelectToken(selectPath);
                if (selectToken != null)
                {
                    if (selectToken is JValue && selectToken.Parent is JProperty property)
                    {
                        fullNodeText = $"{property.Name}:{selectToken}";
                    }
                    else if (selectToken.Type != JTokenType.Object && selectToken.Type != JTokenType.Array)
                    {
                        fullNodeText = selectToken.ToString();
                    }
                }
            }
            catch
            {
                // 경로 해석 실패 시 기본 노드 텍스트를 그대로 사용
            }

            return $"NODE: {fullNodeText}{Environment.NewLine}PATH: {nodePath}";
        }

        private void ScrollRichTextToNode(TreeNode node)
        {
            if (jsonObject == null || string.IsNullOrWhiteSpace(richTextBox_json.Text))
                return;

            try
            {
                string selectPath = Select_json_path(node, Path_skip_mode.Skip).Replace("/", ".");
                if (selectPath.Contains(":"))
                {
                    selectPath = selectPath.Substring(0, selectPath.LastIndexOf(':'));
                }

                JToken selectToken = selectPath == "Root" ? jsonObject.SelectToken("$") : jsonObject.SelectToken(selectPath);
                string searchText = string.Empty;

                if (selectToken?.Parent is JProperty property)
                {
                    searchText = $"\"{property.Name}\"";
                }
                else
                {
                    searchText = selectToken?.ToString() ?? string.Empty;
                }

                if (string.IsNullOrWhiteSpace(searchText))
                    return;

                int startIndex = richTextBox_json.Text.IndexOf(searchText, StringComparison.Ordinal);
                if (startIndex < 0)
                    return;

                richTextBox_json.Select(startIndex, searchText.Length);
                richTextBox_json.ScrollToCaret();
            }
            catch
            {
                // 선택 노드와 RichTextBox 매칭 실패 시 무시
            }
        }

        private string ToNodePreview(string fullText)
        {
            if (string.IsNullOrEmpty(fullText))
                return fullText;
            if (fullText.Length <= NodePreviewMaxLength)
                return fullText;
            return fullText.Substring(0, NodePreviewMaxLength) + "...";
        }

        private string GetFullNodeText(TreeNode node)
        {
            if (node?.Tag is string fullText && !string.IsNullOrWhiteSpace(fullText))
                return fullText;
            return node?.Text ?? string.Empty;
        }

        private void TreeView_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                TreeNode node = treeViewJson.GetNodeAt(e.X, e.Y);
                treeViewJson.SelectedNode = e.Node;
                contextMenuStrip1.Show(treeViewJson, e.Location);
            }
        }

        private void treeViewJson_BeforeLabelEdit(object sender, NodeLabelEditEventArgs e)
        {
            if (!isF2EditRequested)
            {
                e.CancelEdit = true;
                return;
            }

            BeginInvoke(new Action(() =>
            {
                if (e.Node == null)
                    return;

                IntPtr editHandle = SendMessage(treeViewJson.Handle, TVM_GETEDITCONTROL, IntPtr.Zero, IntPtr.Zero);
                if (editHandle == IntPtr.Zero)
                    return;

                Rectangle bounds = e.Node.Bounds;
                int editX = bounds.Left;
                int editY = bounds.Top;
                int editWidth = Math.Max(300, treeViewJson.ClientSize.Width - editX - 8);
                int editHeight = Math.Max(bounds.Height + 6, 24);

                MoveWindow(editHandle, editX, editY, editWidth, editHeight, true);
            }));
        }

        private void objectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                if (treeViewJson.SelectedNode != null)
                {
                    string nodes_cnt = string.Empty;
                    string select_node_path = string.Empty;
                    string temp_object_str = string.Empty;

                    string result_path = Select_json_path(treeViewJson.SelectedNode, Path_skip_mode.Skip);
                    result_path = result_path.Replace("/", ".");
                    if (result_path.Contains(":"))
                    {
                        MessageBox.Show(this, "해당 요소에는 추가할 수 없습니다!", "WARNING");
                        return;
                    }

                    JToken select_token;

                    if (result_path == "Root")
                    {
                        select_token = jsonObject.SelectToken("$");
                    }
                    else
                    {
                        select_token = jsonObject.SelectToken(result_path);
                    }



                    if (select_token.Type == JTokenType.Object)
                        temp_object_str = "NEW_OBJECT" + (treeViewJson.SelectedNode.Nodes.Count + 1).ToString(nodes_cnt);
                    else if (select_token.Type == JTokenType.Array)
                        temp_object_str = "[" + (treeViewJson.SelectedNode.Nodes.Count).ToString(nodes_cnt) + "]";
                    else
                    {
                        MessageBox.Show(this, "해당 요소에는 추가할 수 없습니다!", "WARNING");
                        return;
                    }

                    TreeNode newNode = new TreeNode(temp_object_str);
                    newNode.ToolTipText = newNode.Text;
                    newNode.Tag = temp_object_str;
                    treeViewJson.SelectedNode.Nodes.Add(newNode);

                    Add_Json(jsonObject, treeViewJson.SelectedNode, json_type.Obejct);

                    newNode.ImageIndex = 0;
                    newNode.SelectedImageIndex = 0;
                    treeViewJson.SelectedNode.Expand();
                }
            }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException || ex is Newtonsoft.Json.JsonException)
            {
                MessageBox.Show(ex.Message, "WARNING");
            }

        }
        private void valueToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                if (treeViewJson.SelectedNode != null)
                {
                    string nodes_cnt = string.Empty;
                    string select_node_path = string.Empty;
                    string result_path = Select_json_path(treeViewJson.SelectedNode, Path_skip_mode.Skip);
                    result_path = result_path.Replace("/", ".");
                    if (result_path.Contains(":"))
                    {
                        MessageBox.Show(this, "해당 요소에는 추가할 수 없습니다!", "WARNING");
                        return;
                    }
                    JToken select_token;

                    if (result_path == "Root")
                    {
                        select_token = jsonObject.SelectToken("$");
                    }
                    else
                    {
                        select_token = jsonObject.SelectToken(result_path);
                    }

                    if (select_token.Type != JTokenType.Object && select_token.Type == JTokenType.Array)
                    {
                        MessageBox.Show(this, "해당 요소에는 추가할 수 없습니다!", "WARNING");
                        return;
                    }

                    string temp_key_str = "NEW_KEY" + (treeViewJson.SelectedNode.Nodes.Count + 1).ToString(nodes_cnt) + ":" + "NEW_VALUE" + (treeViewJson.SelectedNode.Nodes.Count + 1).ToString(nodes_cnt);
                    TreeNode newNode = new TreeNode(temp_key_str);
                    newNode.ToolTipText = newNode.Text;
                    newNode.Tag = temp_key_str;
                    treeViewJson.SelectedNode.Nodes.Add(newNode);

                    Add_Json(jsonObject, treeViewJson.SelectedNode, json_type.Value);

                    newNode.ImageIndex = 1;
                    newNode.SelectedImageIndex = 1;
                    treeViewJson.SelectedNode.Expand();


                }
            }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException || ex is Newtonsoft.Json.JsonException)
            {
                MessageBox.Show(ex.Message, "WARNING");
            }

        }

        private void arrayToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                if (treeViewJson.SelectedNode != null)
                {
                    string nodes_cnt = string.Empty;
                    string select_node_path = string.Empty;

                    string result_path = Select_json_path(treeViewJson.SelectedNode, Path_skip_mode.Skip);
                    result_path = result_path.Replace("/", ".");
                    if (result_path.Contains(":"))
                    {
                        MessageBox.Show(this, "해당 요소에는 추가할 수 없습니다!", "WARNING");
                        return;
                    }
                    JToken select_token;
                    if (result_path == "Root")
                    {
                        select_token = jsonObject.SelectToken("$");
                    }
                    else
                    {
                        select_token = jsonObject.SelectToken(result_path);
                    }

                    if (select_token.Type != JTokenType.Object && select_token.Type != JTokenType.Array)
                    {
                        MessageBox.Show(this, "해당 요소에는 추가할 수 없습니다!", "WARNING");
                        return;
                    }

                    string temp_array_str = "NEW_ARRAY" + (treeViewJson.SelectedNode.Nodes.Count + 1).ToString(nodes_cnt);
                    TreeNode newNode = new TreeNode(temp_array_str);
                    newNode.ToolTipText = newNode.Text;
                    newNode.Tag = temp_array_str;
                    treeViewJson.SelectedNode.Nodes.Add(newNode);


                    Add_Json(jsonObject, treeViewJson.SelectedNode, json_type.Array);

                    newNode.ImageIndex = 2;
                    newNode.SelectedImageIndex = 2;
                    treeViewJson.SelectedNode.Expand();
                }
            }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException || ex is Newtonsoft.Json.JsonException)
            {
                MessageBox.Show(ex.Message, "WARNING");
            }

        }

        private void deleteNodeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("선택하신 항목이 삭제됩니다", "Delete", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                try
                {
                    if (treeViewJson.SelectedNode != null)
                    {
                        TreeNode node = treeViewJson.SelectedNode;

                        string node_path = GetNodePath(node, Get_node_mode.Delete);
                        node_path = Regex.Replace(node_path, @"\([^)]*\)", "");
                        node_path = node_path.Split(':')[0];
                        node_path = node_path.Substring(5);

                        node_path = node_path.Replace("/[", "[");
                        node_path = node_path.Replace("/", ".");
                        JToken select_token = jsonObject.SelectToken(node_path);
                        JArray array = new JArray();

                        if (node_path.EndsWith("]"))
                        {
                            string reuslt = Regex.Replace(node_path, @"\[[^\[]*\]$", "");
                            array = (JArray)jsonObject.SelectToken(reuslt);

                            int endIndex = node_path.LastIndexOf(']');

                            int startIndex = node_path.LastIndexOf('[', endIndex);

                            string res_arr_num = node_path.Substring(startIndex + 1, endIndex - startIndex - 1);
                            // 두 번째 객체를 삭제
                            if (array.Count >= 1)
                            {
                                array.RemoveAt(Convert.ToInt32(res_arr_num));
                            }
                        }
                        else
                        {
                            try
                            {
                                select_token.Parent.Remove();
                            }
                            catch (System.NullReferenceException ex)
                            {
                                //MessageBox.Show(this, "삭제가 정상적으로 동작하지 않았습니다!!!", "WARNING");
                            }

                        }
                        treeViewJson.SelectedNode.Remove();
                    }
                }
                catch (System.ArgumentException ex)
                {
                    MessageBox.Show(ex.Message, "WARNING");
                }
            }
            else
            {

            }

        }

        private void copyNodeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            NodeSource = treeViewJson.SelectedNode;
        }


        private void pasteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                NodeTarget = treeViewJson.SelectedNode;
                if (NodeSource == null)
                {
                    MessageBox.Show("복사할 대상에 Copy버튼을 누른 후 시도하세요", "WARNING");
                    return;
                }
                CopyNodes(NodeSource, NodeTarget);

                string result_path_sourceNode = Select_json_path(NodeSource, Path_skip_mode.Skip);
                string result_path_targetNode = Select_json_path(NodeTarget, Path_skip_mode.Skip);

                string temp_path_sourceNode = result_path_sourceNode.Replace("/", ".");
                string temp_path_targetNode = result_path_targetNode.Replace("/", ".");

                JToken select_token_sourceNode = jsonObject.SelectToken(temp_path_sourceNode);
                JToken select_token_targetNode = jsonObject.SelectToken(temp_path_targetNode);

                //foreach (JProperty property in select_token_sourceNode.Children<JProperty>())
                //{
                //    select_token_targetNode[property.Name] = property.Value;
                //}

                CopyToken(select_token_sourceNode, select_token_targetNode);

            }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException || ex is Newtonsoft.Json.JsonException)
            {
                MessageBox.Show(ex.Message, "WARNING");
            }
        }


        static void Add_Json(JObject jObject, TreeNode select_node, json_type addType)
        {
            try
            {
                string result_path = Select_json_path(select_node, Path_skip_mode.Skip);
                result_path = result_path.Replace("/", ".");
                JToken select_token;
                if (result_path == "Root")
                {
                    select_token = jObject.SelectToken("$");
                }
                else
                {
                    select_token = jObject.SelectToken(result_path);
                }
                JArray select_array = new JArray();
                if (select_token.Type == JTokenType.Array)
                    select_array = (JArray)jObject.SelectToken(result_path);

                JObject newObject = new JObject();
                JArray newArray = new JArray();


                if (select_token.Type == JTokenType.Object)
                {
                    if (addType == json_type.Obejct)
                    {
                        string member_cnt = string.Empty;
                        select_token["NEW_OBJECT" + (select_token.Count() + 1).ToString(member_cnt)] = newObject;
                    }
                    else if (addType == json_type.Array)
                    {
                        string member_cnt = string.Empty;
                        select_token["NEW_ARRAY" + (select_token.Count() + 1).ToString(member_cnt)] = newArray;
                    }

                    else if (addType == json_type.Value)
                    {
                        string member_cnt = string.Empty;
                        select_token["NEW_KEY" + (select_token.Count() + 1).ToString(member_cnt)] = "NEW_VALUE" + (select_token.Count() + 1).ToString(member_cnt);
                        return;

                    }
                }
                else if (select_token.Type == JTokenType.Array)
                {
                    if (addType == json_type.Obejct)
                    {
                        select_array.Add(newObject);
                        return;
                    }
                    else if (addType == json_type.Array)
                    {
                        select_array.Add(newArray);
                        return;
                    }
                    else if (addType == json_type.Value)
                    {
                        string member_cnt = string.Empty;
                        JObject newKeyValue = new JObject
                    {
                        { "NEW_KEY"+ (select_array.Count+1).ToString(member_cnt), "NEW_VALUE" + (select_array.Count+1).ToString(member_cnt) }
                    };
                        select_array.Add(newKeyValue);
                        return;
                    }
                }
            }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException || ex is Newtonsoft.Json.JsonException)
            {
                MessageBox.Show(ex.Message, "WARNING");
            }
        }

        static string Select_json_path(TreeNode select_node, Path_skip_mode Mode)
        {
            string node_path = string.Empty;
            node_path = GetNodePath(select_node, Get_node_mode.Delete);
            node_path = Regex.Replace(node_path, @"\([^)]*\)", "");
            if (Mode == Path_skip_mode.Skip)
            {
                if (node_path == "Root")
                {

                }
                else
                    node_path = node_path.Substring(5);

                node_path = node_path.Replace(".[", "[");
            }
            else if (Mode == Path_skip_mode.Noskip)
            {
                if (node_path == "Root")
                {

                }
                else
                {
                    node_path = node_path.Split(':')[0];
                    node_path = node_path.Substring(5);
                }
                node_path = node_path.Replace(".[", "[");
            }

            return node_path;
        }

        private void SaveJson()
        {
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string jsonFilePathWithTimestamp = Path.Combine(Path.GetDirectoryName(jsonFilePath), Path.GetFileNameWithoutExtension(jsonFilePath) + "_" + timestamp + Path.GetExtension(jsonFilePath));
            string currentJsonText = jsonObject.ToString();
            string backupSourceText = string.IsNullOrWhiteSpace(BeforeJsonText) ? currentJsonText : BeforeJsonText;

            File.WriteAllText(jsonFilePathWithTimestamp, backupSourceText);
            BeforeJsonText = currentJsonText;
        }

        private void ApplyJsonToCurrentFile()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(jsonFilePath))
                    return;

                string currentJsonText = jsonObject.ToString();
                File.WriteAllText(jsonFilePath, currentJsonText);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException || ex is ArgumentException || ex is NotSupportedException)
            {
                MessageBox.Show(ex.Message, "WARNING");
            }
        }


        static string GetNodePath(TreeNode node, Get_node_mode mode)
        {
            string path = node.Tag is string fullText ? fullText : node.Text;
            switch (mode)
            {
                case Get_node_mode.Inform:
                    while (node.Parent != null)
                    {
                        node = node.Parent;
                        string nodeText = node.Tag is string nodeFullText ? nodeFullText : node.Text;
                        path = nodeText + "->" + path;
                    }
                    break;
                case Get_node_mode.Delete:
                    while (node.Parent != null)
                    {
                        node = node.Parent;
                        string nodeText = node.Tag is string nodeFullText ? nodeFullText : node.Text;
                        path = nodeText + "/" + path;
                    }
                    break;
            }

            return path;
        }

        private void DisplayCurrentResult()
        {
            if (currentIndex >= 0 && currentIndex < searchResults.Count)
            {
                int startIndex = richTextBox_json.Text.IndexOf(searchResults[currentIndex]);
                if (startIndex >= 0)
                {
                    richTextBox_json.Select(startIndex, searchResults[currentIndex].Length);
                    //richTextBox_json.SelectionBackColor = Color.AliceBlue; // 음영을 넣기 위해 배경색을 노란색으로 설정
                    richTextBox_json.ScrollToCaret();
                    //richTextBox_json.Focus();
                    startIndex = 0;
                }
            }
        }

        //===== Treeview 내용 수정 후 실제 Json파일 반영 =======//
        private void treeViewJson_AfterLabelEdit(object sender, NodeLabelEditEventArgs e)
        {
            try
            {
                ApplyNodeLabelEdit(e?.Node ?? treeViewJson.SelectedNode, e?.Label);
            }
            finally
            {
                isF2EditRequested = false;
            }
        }

        private void ApplyNodeLabelEdit(TreeNode editingNode, string new_key_value)
        {
            try
            {
                if (editingNode == null)
                    return;

                if (new_key_value != null)
                {
                    new_key_value = new_key_value.Replace("\r", string.Empty).Replace("\n", string.Empty);
                }

                string select_node_path = string.Empty;

                string result_path = Select_json_path(editingNode, Path_skip_mode.Skip);
                string key_value = string.Empty;

                if (new_key_value == null)
                {
                    return;
                }

                if (result_path.Contains("/"))
                {
                    int Last_Slash_index = result_path.LastIndexOf('/');
                    string temp_key = result_path.Substring(0, Last_Slash_index);

                    if (Last_Slash_index != -1)
                    {
                        key_value = result_path.Substring(Last_Slash_index + 1);
                    }
                }
                else
                    key_value = result_path;

                if (result_path.Contains(":") && !new_key_value.Contains(":"))
                {
                    int existingColonIndex = key_value.IndexOf(':');
                    if (existingColonIndex != -1)
                    {
                        string existingKey = key_value.Substring(0, existingColonIndex);
                        new_key_value = $"{existingKey}:{new_key_value}";
                    }
                }



                if (result_path.Contains(":"))
                {
                    int index = result_path.IndexOf(':');
                    if (index != -1)
                    {
                        result_path = result_path.Substring(0, index);
                    }
                }

                string new_key = string.Empty;
                string new_value = string.Empty;


                result_path = result_path.Replace("/", ".");  //소수점 입력시 select_token 경로명에 algorithm.insp_info. 과 같이 input으로 들어가 이와 혼동 될 수 있어 경로명을 / 식으로 구성했다가 .으로 바꿈 
                JToken select_token = jsonObject.SelectToken(result_path);


                if (key_value != new_key_value)
                {
                    if (key_value.Contains(":"))
                    {
                        int colon_Index = new_key_value.IndexOf(':');

                        if (colon_Index != -1)
                        {
                            new_key = new_key_value.Substring(0, colon_Index);
                            new_value = new_key_value.Substring(colon_Index + 1);
                        }

                        bool isNumeric = double.TryParse(new_value, out double result);

                        if (isNumeric)
                        {
                            //select_token.Replace(Convert.ToInt32(new_value));
                            editingNode.ImageIndex = 3;
                            editingNode.SelectedImageIndex = 3;

                            if (select_token.Parent != null)
                            {
                                JProperty property = (JProperty)select_token.Parent;

                                if (new_value.Contains("."))
                                    property.Replace(new JProperty(new_key, Convert.ToDouble(new_value)));
                                else
                                    property.Replace(new JProperty(new_key, Convert.ToInt32(new_value)));
                            }
                        }
                        else
                        {
                            if (bool.TryParse(new_value, out bool booleanParam))
                            {
                                if (select_token.Parent != null)
                                {
                                    JProperty property = (JProperty)select_token.Parent;
                                    property.Replace(new JProperty(new_key, booleanParam));
                                }
                                editingNode.ImageIndex = 4;
                                editingNode.SelectedImageIndex = 4;
                            }
                            else
                            {
                                //select_token.Replace(new_value);

                                if (select_token.Parent != null)
                                {
                                    JProperty property = (JProperty)select_token.Parent;
                                    property.Replace(new JProperty(new_key, new_value));
                                }
                                editingNode.ImageIndex = 5;
                                editingNode.SelectedImageIndex = 5;
                            }

                        }
                    }
                    else
                    {
                        if (select_token == null)
                            return;

                        if (select_token.Parent != null)
                        {
                            JProperty property = (JProperty)select_token.Parent;
                            property.Replace(new JProperty(new_key_value, property.Value));
                        }
                    }

                }
                editingNode.ToolTipText = editingNode.Text;
                editingNode.Tag = new_key_value;
                editingNode.Text = ToNodePreview(new_key_value);
                editingNode.ToolTipText = new_key_value;
                EnableTreeViewHorizontalScrollBar();
                return;
            }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException || ex is NullReferenceException || ex is Newtonsoft.Json.JsonException)
            {

                if (ex.Message.Contains("already exist"))
                {
                    MessageBox.Show("동일한 항목이 이미 같은 레이어안에 존재합니다!!!" + "\n" + "파일을 다시 클릭해 수정하세요.", "WARNING");
                }
                else
                {
                    MessageBox.Show(ex.Message + "\n" + "파일을 다시 클릭해 수정하세요.", "WARNING");
                }
            }

        }

        private void treeViewJson_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F2 && treeViewJson.SelectedNode != null)
            {
                isF2EditRequested = true;
                TreeNode nodeToEdit = treeViewJson.SelectedNode;
                string fullNodeText = GetFullNodeText(nodeToEdit);
                if (fullNodeText.Contains(":"))
                {
                    e.SuppressKeyPress = true;
                    using (var editDialog = new Form())
                    using (var editTextBox = new RichTextBox())
                    using (var buttonPanel = new Panel())
                    using (var buttonFlowPanel = new FlowLayoutPanel())
                    using (var okButton = new Button())
                    using (var cancelButton = new Button())
                    {
                        editDialog.Text = "Node Edit";
                        editDialog.StartPosition = FormStartPosition.CenterParent;
                        editDialog.Size = new Size(900, 280);

                        editTextBox.Multiline = true;
                        editTextBox.ScrollBars = RichTextBoxScrollBars.Vertical;
                        editTextBox.WordWrap = true;
                        editTextBox.Dock = DockStyle.Fill;
                        editTextBox.Text = fullNodeText;

                        okButton.Text = "OK";
                        okButton.DialogResult = DialogResult.OK;
                        okButton.Size = new Size(80, 30);

                        cancelButton.Text = "Cancel";
                        cancelButton.DialogResult = DialogResult.Cancel;
                        cancelButton.Size = new Size(80, 30);

                        buttonPanel.Dock = DockStyle.Bottom;
                        buttonPanel.Height = 42;
                        buttonPanel.Padding = new Padding(0, 6, 10, 6);

                        buttonFlowPanel.Dock = DockStyle.Right;
                        buttonFlowPanel.FlowDirection = FlowDirection.RightToLeft;
                        buttonFlowPanel.WrapContents = false;
                        buttonFlowPanel.AutoSize = true;
                        buttonFlowPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;

                        buttonFlowPanel.Controls.Add(cancelButton);
                        buttonFlowPanel.Controls.Add(okButton);
                        buttonPanel.Controls.Add(buttonFlowPanel);

                        editDialog.Controls.Add(editTextBox);
                        editDialog.Controls.Add(buttonPanel);
                        editDialog.AcceptButton = okButton;
                        editDialog.CancelButton = cancelButton;

                        if (editDialog.ShowDialog(this) == DialogResult.OK)
                        {
                            ApplyNodeLabelEdit(nodeToEdit, editTextBox.Text);
                        }

                        isF2EditRequested = false;
                    }
                }
                else
                {
                    treeViewJson.SelectedNode.BeginEdit();
                }
            }
        }

        private void Button_Click(object sender, EventArgs e)
        {

            switch (((System.Windows.Forms.Button)sender).Name)
            {
                case "BtnOpenFile":

                    OpenFileDialog openFileDialog = new OpenFileDialog();
                    openFileDialog.Filter = "All Files (*.*)|*.*";
                    openFileDialog.Title = "파일 열기";

                    if (openFileDialog.ShowDialog() == DialogResult.OK)
                    {
                        jsonFilePath = openFileDialog.FileName;
                        SaveFilePath(jsonFilePath);
                        string jsonDirectory = Path.GetDirectoryName(jsonFilePath);
                        if (!string.IsNullOrWhiteSpace(jsonDirectory))
                        {
                            SaveFolderPath(jsonDirectory);
                            LoadFilesToDataGridView(jsonDirectory);
                        }
                    }
                    LoadJson(jsonFilePath);
                    PopulateTreeView();

                    break;

                case "BtnApply":
                    ApplyJsonToCurrentFile();
                    string jsonString = jsonObject.ToString();
                    richTextBox_json.Text = jsonString;
                    break;

                case "BtnSave":
                    if (MessageBox.Show("수정한 내용을 Apply 했는지 확인하고 저장해주세요", "Save", MessageBoxButtons.YesNo) == DialogResult.Yes)
                    {
                        SaveJson();
                        JsonfolderPath = Path.GetDirectoryName(jsonFilePath);
                        if (string.IsNullOrWhiteSpace(JsonfolderPath))
                        {
                            JsonfolderPath = ReadSettingPath(settingsFolderPath);
                        }

                        if (!string.IsNullOrWhiteSpace(JsonfolderPath))
                        {
                            SaveFolderPath(JsonfolderPath);
                            LoadFilesToDataGridView(JsonfolderPath);
                        }
                    }
                    else
                    {

                    }
                    break;
                //case "BtnSearch":
                //    treeViewJson.Focus();
                //    string searchText = textBoxSearch.Text.ToLower();
                //    ExpandAllNodes(treeViewJson.Nodes);
                //    TreeNode foundNode = FindNode(treeViewJson.Nodes, searchText);
                //    if (foundNode != null)
                //    {
                //        treeViewJson.SelectedNode = foundNode;
                //        //foundNode.EnsureVisible();
                //        treeViewJson.Focus();
                //    }
                //    else
                //    {
                //        MessageBox.Show("Node not found.", "WARNING");
                //    }
                //    ////////////////

                //    searchResults = richTextBox_json.Lines.Where(line => line.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                //    currentIndex = 0;
                //    DisplayCurrentResult();

                //    break;

                //case "BtnSearchNext":
                //    string searchNextText = textBoxSearch.Text.ToLower();
                //    TreeNode startNodeA = treeViewJson.SelectedNode != null ? treeViewJson.SelectedNode.NextVisibleNode : treeViewJson.Nodes[0];
                //    TreeNode foundNextNode = FindNextNode(startNodeA, searchNextText);
                //    if (foundNextNode != null)
                //    {
                //        treeViewJson.SelectedNode = foundNextNode;
                //        //foundNextNode.EnsureVisible();
                //        treeViewJson.Focus();
                //    }
                //    else
                //    {
                //        MessageBox.Show("No more nodes found.", "WARNING");
                //    }
                //    ////////////////////
                //    if (currentIndex < searchResults.Count - 1)
                //    {
                //        currentIndex++;
                //        DisplayCurrentResult();
                //    }
                //    break;

                //case "BtnSearchPrevious":

                //    string searchPrevText = textBoxSearch.Text.ToLower();
                //    TreeNode startNodeB = treeViewJson.SelectedNode != null ? treeViewJson.SelectedNode.PrevVisibleNode : null;
                //    TreeNode foundPrevNode = FindPrevNode(startNodeB, searchPrevText);
                //    if (foundPrevNode != null)
                //    {
                //        treeViewJson.SelectedNode = foundPrevNode;
                //        //foundPrevNode.EnsureVisible();
                //        treeViewJson.Focus();
                //    }
                //    else
                //    {
                //        MessageBox.Show("No previous nodes found.", "WARNING");
                //    }

                //    ////////////////////
                //    if (currentIndex > 0)
                //    {
                //        currentIndex--;
                //        DisplayCurrentResult();
                //    }
                //    break;
                case "BtnOpenFolder":

                    using (var folderDialog = new CommonOpenFileDialog())
                    {
                        folderDialog.IsFolderPicker = true;
                        if (folderDialog.ShowDialog() == CommonFileDialogResult.Ok)
                        {
                            JsonfolderPath = folderDialog.FileName;
                            SaveFolderPath(JsonfolderPath);
                            LoadFilesToDataGridView(JsonfolderPath);
                        }
                    }

                    break;

                case "BtnExpandAll":

                    ExpandAllNodes(treeViewJson.Nodes);

                    break;
            }

        }

        static void SaveFilePath(string path)
        {
            WriteSettingPath(settingsFilePath, path);
        }

        static void SaveFolderPath(string path)
        {
            WriteSettingPath(settingsFolderPath, path);
        }

        static string ReadSettingPath(string settingPath)
        {
            if (!File.Exists(settingPath))
                return string.Empty;

            string path = File.ReadAllText(settingPath, settingsEncoding).Trim();
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            try
            {
                return Path.GetFullPath(path);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException)
            {
                return string.Empty;
            }
        }

        static void WriteSettingPath(string settingPath, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            string normalizedPath = path.Trim();
            try
            {
                normalizedPath = Path.GetFullPath(normalizedPath);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException)
            {
                return;
            }

            File.WriteAllText(settingPath, normalizedPath, settingsEncoding);
        }



        private void LoadFilesToDataGridView(string folderPath)
        {
            if (Directory.Exists(folderPath))
            {
                //string[] files = Directory.GetFiles(folderPath);
                DirectoryInfo di = new DirectoryInfo(folderPath);
                FileInfo[] files = di.GetFiles("*.json");

                DataTable dataTable = new DataTable();
                dataTable.Columns.Add("파일명");
                dataTable.Columns.Add("날짜");
                dataTable.Columns.Add("경로");

                foreach (var file in files)
                {
                    DataRow row = dataTable.NewRow();
                    row["파일명"] = file.Name;
                    row["날짜"] = file.LastWriteTime;
                    row["경로"] = file.FullName;
                    dataTable.Rows.Add(row);
                }

                dataGridViewJson.DataSource = dataTable;
                dataGridViewJson.Sort(dataGridViewJson.Columns[1], ListSortDirection.Descending);
            }
            else
            {
                MessageBox.Show("지정된 경로가 존재하지 않습니다.", "WARNING");
            }
        }

        private void dataGridViewJson_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            string datagridview_jsonpath = dataGridViewJson.Rows[e.RowIndex].Cells[2].FormattedValue.ToString();
            jsonFilePath = datagridview_jsonpath;
            SaveFilePath(datagridview_jsonpath);
            string selectedFolderPath = Path.GetDirectoryName(datagridview_jsonpath);
            if (!string.IsNullOrWhiteSpace(selectedFolderPath))
            {
                SaveFolderPath(selectedFolderPath);
            }
            LoadJson(datagridview_jsonpath);
            PopulateTreeView();
        }

        private void SearchButton_Click(object sender, EventArgs e)
        {
            //string searchText = textBoxSearch.Text;
            //string richText = richTextBox_json.Text;

            //listView_SearchResult.Items.Clear();
            //listView_SearchResult.View = View.List;

            //int startIndex = 0;
            //int OrderNum = 0;
            //while ((startIndex = richText.IndexOf(searchText, startIndex)) != -1)
            //{
            //    listView_SearchResult.Items.Add(new System.Windows.Forms.ListViewItem($"위치: {OrderNum + 1}, 텍스트: {searchText}"));
            //    startIndex += searchText.Length;
            //    OrderNum++;
            //}
        }

        private void CopyNodes(TreeNode sourceNode, TreeNode targetNode)
        {

            foreach (TreeNode childNode in sourceNode.Nodes)
            {
                TreeNode newNode = new TreeNode(childNode.Text);
                newNode.ToolTipText = childNode.Text;
                newNode.Tag = childNode.Tag;
                newNode.ImageIndex = childNode.ImageIndex;
                newNode.SelectedImageIndex = childNode.SelectedImageIndex;

                targetNode.Nodes.Add(newNode);
                CopyNodes(childNode, newNode);
            }

        }

        private void UpdateNodeToolTips(TreeNode node)
        {
            node.ToolTipText = node.Text;
            foreach (TreeNode child in node.Nodes)
            {
                UpdateNodeToolTips(child);
            }
        }



        void CopyToken(JToken source, JToken target)
        {
            if (source is JObject sourceObject && target is JObject targetObject)
            {
                foreach (JProperty property in sourceObject.Properties())
                {
                    if (property.Value is JArray)
                    {
                        targetObject[property.Name] = new JArray(property.Value);
                    }
                    else if (property.Value is JObject)
                    {
                        targetObject[property.Name] = new JObject();
                        CopyToken(property.Value, targetObject[property.Name]);
                    }
                    else
                    {
                        targetObject[property.Name] = property.Value;
                    }
                }
            }
            else if (source is JArray sourceArray && target is JArray targetArray)
            {
                foreach (JToken item in sourceArray)
                {
                    if (item is JObject)
                    {
                        JObject newObject = new JObject();
                        CopyToken(item, newObject);
                        targetArray.Add(newObject);
                    }
                    else if (item is JArray)
                    {
                        JArray newArray = new JArray();
                        CopyToken(item, newArray);
                        targetArray.Add(newArray);
                    }
                    else
                    {
                        targetArray.Add(item);
                    }
                }
            }
        }

        private void CreateCustomTitleBar()
        {
            // 제목 표시줄 패널
            const int color = 30;
            titleBar = new System.Windows.Forms.Panel
            {
                Dock = DockStyle.None,
                Height = 30,
                Width = 90,
                BackColor = System.Drawing.Color.FromArgb(255, color, color, color) // 어두운 배경색
            };
            // 종료 버튼
            System.Windows.Forms.Button closeButton = new System.Windows.Forms.Button
            {
                Text = "X",
                Font = new System.Drawing.Font("Segoe UI", 12F, GraphicsUnit.Point),
                Dock = DockStyle.Right,
                Width = 30,
                ForeColor = System.Drawing.Color.White,
                BackColor = System.Drawing.Color.FromArgb(255, color, color, color),
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 }
            };
            closeButton.Click += (sender, e) => this.Hide();

            // 최소화 버튼
            System.Windows.Forms.Button minimizeButton = new System.Windows.Forms.Button
            {
                Text = "➖",
                Font = new System.Drawing.Font("굴림", 10F, GraphicsUnit.Point),
                Dock = DockStyle.Right,
                Width = 30,
                ForeColor = System.Drawing.Color.White,
                BackColor = System.Drawing.Color.FromArgb(255, color, color, color),
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 }
            };
            minimizeButton.Click += (sender, e) => this.WindowState = FormWindowState.Minimized;

            // 최소화 버튼
            System.Windows.Forms.Button maximizeButton = new System.Windows.Forms.Button
            {
                Text = "□",
                Font = new System.Drawing.Font("굴림", 9F, GraphicsUnit.Point),
                Dock = DockStyle.Right,
                Width = 30,
                ForeColor = System.Drawing.Color.White,
                BackColor = System.Drawing.Color.FromArgb(255, color, color, color),
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 }
            };
            maximizeButton.Click += (sender, e) =>
            {
                if (this.WindowState == FormWindowState.Normal)
                {
                    this.WindowState = FormWindowState.Maximized;
                    maximizeButton.Text = "⎍";
                    maximizeButton.Font = new System.Drawing.Font("굴림", 15F, GraphicsUnit.Point);
                }
                else
                {
                    this.WindowState = FormWindowState.Normal;
                    maximizeButton.Text = "□";
                    maximizeButton.Font = new System.Drawing.Font("굴림", 9F, GraphicsUnit.Point);
                }
            };

            // 제목 표시줄 패널에 컨트롤 추가
            titleBar.Controls.Add(minimizeButton);
            titleBar.Controls.Add(maximizeButton);
            titleBar.Controls.Add(closeButton);

            // 폼에 제목 표시줄 패널 추가
            this.Controls.Add(titleBar);
            UpdateTitleBarPosition();
        }

        private void UpdateTitleBarPosition()
        {
            if (titleBar != null)
            {
                titleBar.Location = new System.Drawing.Point(this.ClientSize.Width - titleBar.Width - 2, 5);
            }
        }

        private void Form_Resize(object sender, EventArgs e)
        {
            // Form 크기가 변경될 때 제목 표시줄 위치 업데이트
            UpdateTitleBarPosition();
        }

        private void AppendInspectionLog(string message)
        {
            // 현재 시간을 포함한 로그 메시지 생성
            string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";

            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() =>
                {
                    // RichTextBox에 메시지 추가
                    rtbNodeLocation.AppendText(logMessage + "\n");

                    // 스크롤을 가장 아래로 이동
                    rtbNodeLocation.SelectionStart = rtbNodeLocation.TextLength;
                    rtbNodeLocation.ScrollToCaret();
                }));
            }
            else
            {
                // RichTextBox에 메시지 추가
                rtbNodeLocation.AppendText(logMessage + "\n");

                // 스크롤을 가장 아래로 이동
                rtbNodeLocation.SelectionStart = rtbNodeLocation.TextLength;
                rtbNodeLocation.ScrollToCaret();
            }


        }
    }


}
