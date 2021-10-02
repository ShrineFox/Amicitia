﻿using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.IO;
using Amicitia.ResourceWrappers;
using AmicitiaLibrary.Graphics;
using OpenTK;
using OpenTK.Input;
using System.Runtime.InteropServices;
using AmicitiaLibrary.Graphics.RenderWare;
using System.Drawing;
using System.Linq;
using System.Numerics;
using Amicitia.Utilities;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using MetroSet_UI.Enums;
using MetroSet_UI.Forms;
using System.Threading;

namespace Amicitia
{
    internal partial class MainForm : MetroSetForm
    {
        //private static Rectangle _lastMainTreeViewSize;
        private static MainForm mInstance;
        private ModelViewer.ModelViewer mViewer;
        public static string openedPath;

        public static MainForm Instance
        {
            get => mInstance;
            set
            {
                if (mInstance != null)
                {
                    throw new Exception("Instance already exists!");
                }

                mInstance = value;
            }
        }

        public static TreeView MainTreeView => mainTreeView;

        public static PropertyGrid MainPropertyGrid => Instance.mainPropertyGrid;

        public static PictureBox MainPictureBox => Instance.mainPictureBox;

        public static GLControl GLControl => Instance.glControl1;

        public MainForm()
        {
            InitializeComponent();
            InitializeMainForm();
#if DEBUG
            Type t = Type.GetType("Mono.Runtime");
            if (t != null)
                Console.WriteLine("You are running with the Mono VM");
            else
                NativeMethods.AllocConsole();
#endif
            mainPictureBox.BackColor = Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(30)))), ((int)(((byte)(30)))));
        }

        // Events
        private void MainFormDragEnterEventHandler(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
            else
                e.Effect = DragDropEffects.None;
        }

        private void MainFormDragDropEventHandler(object sender, DragEventArgs e)
        {
            string[] filePaths = (string[])e.Data.GetData(DataFormats.FileDrop, false);

            if (filePaths.Length != 0)
            {
                OpenFile(filePaths[0]);
            }
        }

        private void MainTreeViewAfterLabelEditEventHandler(object sender, NodeLabelEditEventArgs e)
        {
            mainTreeView.LabelEdit = false;
        }

        private void MainTreeViewKeyDownEventHandler(object sender, KeyEventArgs e)
        {
            if (e.Control)
            {
                HandleTreeViewCtrlShortcuts(e.KeyData);
            }
        }

        private void MainTreeViewAfterSelectEventHandler(object sender, TreeViewEventArgs e)
        {
            // hide the picture box
            mainPictureBox.Visible = false;
            glControl1.Visible = false;

            var resNode = mainTreeView.SelectedNode;
            var resWrap = resNode as IResourceWrapper;

            if ( resWrap == null )
            {
                return;
            }

            try
            {
                mainPropertyGrid.SelectedObject = resWrap;
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
                mainPropertyGrid.SelectedObject = resWrap;
            }

            if (mViewer != null)
            {
                mViewer.BgColor = Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(30)))), ((int)(((byte)(30)))));
                if (resWrap.FileType == SupportedFileType.RmdScene || resWrap.FileType == SupportedFileType.RwClumpNode || resWrap.FileType == SupportedFileType.RwGeometryNode || resWrap.FileType == SupportedFileType.RwAtomicNode)
                {
                    glControl1.Visible = true;

                    try
                    {
                        mViewer.DeleteScene();

                        if ( resWrap.FileType == SupportedFileType.RmdScene )
                        {
                            var scene = resWrap.Resource as RmdScene;

                            // For field models
                            if (!scene.HasTextureDictionary && resNode.Parent != null)
                            {
                                foreach ( TreeNode node in resNode.Parent.Nodes )
                                {
                                    if (node.Text.EndsWith("rws"))
                                    {
                                        var parentResWrap = ( IResourceWrapper )node;
                                        var parentScene = parentResWrap.Resource as RmdScene;
                                        if ( parentScene.HasTextureDictionary )
                                        {
                                            mViewer.LoadTextures(parentScene.TextureDictionary);
                                        }
                                    }
                                }
                            }

                            mViewer.LoadScene( resWrap.Resource as RmdScene );
                        }
                        else if ( resWrap.FileType == SupportedFileType.RwClumpNode )
                        {
                            var model = ( RwClumpNode )resWrap.Resource;
                            var scene = model.FindParent( RwNodeId.RmdSceneNode ) as RmdScene;
                            if ( scene != null && scene.HasTextureDictionary )
                                mViewer.LoadTextures( scene.TextureDictionary );

                            mViewer.LoadModel( model );
                        }
                        else if ( resWrap.FileType == SupportedFileType.RwGeometryNode )
                        {
                            var geometry = ( RwGeometryNode )resWrap.Resource;
                            var scene = geometry.FindParent( RwNodeId.RmdSceneNode ) as RmdScene;
                            if ( scene != null && scene.TextureDictionary != null )
                                mViewer.LoadTextures( scene.TextureDictionary );

                            mViewer.LoadGeometry( geometry, Matrix4x4.Identity );
                        }
                        else if ( resWrap.FileType == SupportedFileType.RwAtomicNode )
                        {
                            var atomicNode = ( RwAtomicNode )resWrap.Resource;
                            var clump = atomicNode.FindParent( RwNodeId.RwClumpNode ) as RwClumpNode;
                            var geometry = clump.GeometryList[atomicNode.GeometryIndex];
                            var frame = clump.FrameList[atomicNode.FrameIndex];

                            var scene = atomicNode.FindParent( RwNodeId.RmdSceneNode ) as RmdScene;
                            if ( scene != null && scene.TextureDictionary != null )
                                mViewer.LoadTextures( scene.TextureDictionary );

                            mViewer.LoadGeometry( geometry, frame.WorldTransform );
                        }
                    }
                    catch ( Exception ex )
                    {
                        Console.WriteLine( ex.Message );
                        //throw;
                    }

                    glControl1.Focus();
                    glControl1.Invalidate();
                    glControl1.Update();
                }
                else
                {
                    mViewer.DeleteScene();
                }
            }

            // Check if the resource is a texture
            if (resWrap.Resource is ITextureFile)
            {
                // Unhide the picture box if we have a picture selected
                mainPictureBox.Visible = true;
                mainPictureBox.Image = ((ITextureFile)resWrap.Resource).GetBitmap();
            }
        }

        private void MainTreeViewNodeMouseClickEventHandler(object sender, TreeNodeMouseClickEventArgs e)
        {
            // Setting this will make all mouse clicks select a node, as opposed to only left clicks
            mainTreeView.SelectedNode = e.Node;
        }

        private void OpenToolStripMenuItemClickEventHandler(object sender, EventArgs e)
        {
            // hide file dropdown
            fileToolStripMenuItem.HideDropDown();

            using (var centeringService = new DialogCenteringService(this))
            using (OpenFileDialog openFileDlg = new OpenFileDialog())
            {
                openFileDlg.Filter = SupportedFileManager.FileFilter;
                // Exit out if user didn't select a file
                if (openFileDlg.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                OpenFile( openFileDlg.FileName);
            }
        }

        private void SaveToolStripMenuItemClickEventHandler(object sender, EventArgs e)
        {
            if (mainTreeView.Nodes.Count == 0)
                return;

            var wrapper = (IResourceWrapper)mainTreeView.Nodes[0];

            if (wrapper.Export(openedPath, wrapper.FileType))
            {
                using (var centeringService = new DialogCenteringService(this))
                    MessageBox.Show("File has been saved successfully.", "Success");
            }
        }

        private void SaveAsToolStripMenuItemClickEventHandler( object sender, EventArgs e )
        {
            if ( mainTreeView.Nodes.Count == 0 )
                return;

            if ( ( ( IResourceWrapper )mainTreeView.Nodes[0] ).Export() )
            {
                using ( var centeringService = new DialogCenteringService( this ) )
                    MessageBox.Show( "File has been saved successfully.", "Success" );
            }
        }

        internal void UpdateReferences()
        {
            MainTreeViewAfterSelectEventHandler(this, new TreeViewEventArgs(mainTreeView.SelectedNode));
        }

        // Initializer
        private void InitializeMainForm()
        {
            // this
            Text = Program.TitleString;
            Instance = this;
            DragDrop += MainFormDragDropEventHandler;
            DragEnter += MainFormDragEnterEventHandler;

            // mainTreeView
            mainTreeView.AfterSelect += MainTreeViewAfterSelectEventHandler;
            mainTreeView.KeyDown += MainTreeViewKeyDownEventHandler;
            mainTreeView.AfterLabelEdit += MainTreeViewAfterLabelEditEventHandler;
            mainTreeView.NodeMouseClick += MainTreeViewNodeMouseClickEventHandler;
            //mainTreeView.SizeChanged += MainTreeView_SizeChanged;
            mainTreeView.AllowDrop = false;
            mainTreeView.DragDrop += MainFormDragDropEventHandler;
            mainTreeView.DragEnter += MainFormDragEnterEventHandler;
            
            // mainPictureBox
            mainPictureBox.Visible = false;

            // mainPropertyGrid
            //mainPropertyGrid.PropertySort = PropertySort.Categorized;
            mainPropertyGrid.PropertySort = PropertySort.NoSort;
            mainPropertyGrid.PropertyValueChanged += ( s, e ) => MainTreeViewAfterSelectEventHandler(s, new TreeViewEventArgs(MainTreeView.SelectedNode));
        }

        // Handlers
        private void HandleTreeViewCtrlShortcuts(Keys keys)
        {
            bool handled = false;

            foreach ( ToolStripItem item in mainMenuStrip.Items )
            {
                var menuItem = item as ToolStripMenuItem;
                if ( menuItem != null )
                {
                    if (menuItem.ShortcutKeys == keys)
                    {
                        menuItem.PerformClick();
                        handled = true;
                    }
                }
            }

            if ( handled )
                return;

            if (mainTreeView.SelectedNode != null && mainTreeView.SelectedNode.ContextMenuStrip != null)
            {
                foreach (ToolStripItem item in mainTreeView.SelectedNode.ContextMenuStrip.Items)
                {
                    var menuItem = item as ToolStripMenuItem;
                    if (menuItem != null)
                    {
                        if (menuItem.ShortcutKeys == keys)
                            menuItem.PerformClick();
                    }
                }
            }
        }

        private void ClearForm()
        {
            // destroy the model scene if it's still loaded
            if (mViewer != null)
                mViewer.DeleteScene();

            // Hide the picture box so a possibly last selected image doesn't stay visible
            if (mainPictureBox.Visible)
            {
                mainPictureBox.Visible = false;
            }

            // Clear nodes as we don't want multiple hierarchies
            if (mainTreeView.Nodes.Count != 0)
            {
                mainTreeView.Nodes.Clear();
            }
        }

        public void OpenFile(string path)
        {
            // Get the supported file index so we can check if it's /actually/ supported as you can override the filter easily by copy pasting
            int supportedFileIndex = SupportedFileManager.GetSupportedFileIndex(path);

            if (supportedFileIndex == -1)
            {
                return;
            }

            // clear the form of loaded resources
            ClearForm();

            TreeNode treeNode;

#if !DEBUG
            try
            {
#endif
            // Get the resource from the factory and it to the tree view
            using ( var fileStream = File.OpenRead( path ) )
                treeNode = ( TreeNode )ResourceWrapperFactory.GetResourceWrapper( Path.GetFileName( path ), fileStream, supportedFileIndex, path );
#if !DEBUG
            }
            catch (Exception)
            {
                using (var centeringService = new DialogCenteringService(this))
                    MessageBox.Show("Can't open this file format.", "Open file error", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly);
                return;
            }
#endif

            mainTreeView.BeginUpdate();
            {
                mainTreeView.Nodes.Add(treeNode);
                mainTreeView.SelectedNode = mainTreeView.TopNode;
            }
            mainTreeView.EndUpdate();

            mainTreeView.Select();
            if (mainTreeView.Nodes.Count > 0)
            {
                mainTreeView.SelectedNode = mainTreeView.Nodes[0];
                mainTreeView.SelectedNode.Expand();
                
                if (mainTreeView.Nodes[0].Nodes.Count > 0)
                    for (int i = 0; i < mainTreeView.SelectedNode.Nodes.Count; i++)
                        mainTreeView.SelectedNode.Nodes[i].Expand();
            }
            openedPath = path;
        }

        private void GlControl1_Load(object sender, EventArgs e)
        {
            glControl1.Visible = false;

            if (ModelViewer.ModelViewer.IsSupported)
                mViewer = new ModelViewer.ModelViewer(glControl1);
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (mViewer != null)
                mViewer.DisposeViewer();
        }

        private void OptionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (mViewer == null)
                return;

            Form options = new Form();
            Label colorlab = new Label();
            Label more = new Label();
            Button picker = new Button();
            options.Text = "Options";
            more.Text = "Stay tuned...";
            more.Location = new Point(16, 80);
            more.ForeColor = Color.Gray;
            more.Font = new Font(more.Font, FontStyle.Italic);
            picker.Text = "...";
            picker.Location = new System.Drawing.Point(140, 16);
            picker.Width /= 3;
            picker.Click += (object s, EventArgs ev) =>
            {
                ColorDialog d = new ColorDialog {Color = mViewer.BgColor};
                d.ShowDialog(options);
                mViewer.BgColor = d.Color;
            };
            colorlab.Text = "Model viewer background color";
            colorlab.Location = new Point(16, 20);
            colorlab.Width = 200;
            options.Size = new Size(512, 256);
            options.Controls.Add(picker);
            options.Controls.Add(colorlab);
            options.Controls.Add(more);
            options.ShowDialog(this);
        }

        private void CloseToolStripMenuItemClickEventHandler(object sender, EventArgs e)
        {
            this.Close();
        }
    }

    internal static class NativeMethods
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool AllocConsole();
        public static bool GetAsyncKey(Keys key)
        {
            if (MainForm.GLControl.Focused)
                return GetAsyncKeyState(key) != 0;
            else
                return false;
        }

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(Keys vKey);
    }
}
