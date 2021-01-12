﻿using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Globalization;
using System.Threading;
using GeneralDef;
using NETSDKHelper;
using System.Diagnostics;
using System.Reflection;


namespace NetDemo
{
    public partial class NetDemo : Form
    {
        /******************* Define member variables start *******************************/
        
        PlayPanel[] arrayRealPanel = new PlayPanel[NETDEMO.REAL_PANEL_MAX_SIZE];

        static readonly object locker = new object();
        private List<DeviceInfo> m_deviceInfoList = new List<DeviceInfo>();

        static readonly object m_notLoggeddeviceInfoListlocker = new object();
        private List<DeviceInfo> m_notLoggeddeviceInfoList = new List<DeviceInfo>();

        PlayPanel m_curRealPanel;

        IntPtr lpCloudDevHandle = IntPtr.Zero;

        PlayPanel m_mourseRightSelectedPanel;/*panel*/
        TreeNode m_mourseRightDeviceNode = null;

        public TreeNodeInfo m_CurSelectTreeNodeInfo = new TreeNodeInfo();

        Int32 m_curRealPanelIndex = 0;

        
        bool realMaxFlag = false;
        bool playBackMaxFlag = false;

        
        int m_layoutPanelWidth = 0;
        int m_layoutPanelHeight = 0;

        PTZControl m_oPtzControl = null;
        AddCloudDevice m_addCloudDevice = null;
        Discovery objDiscovery = null;

        
        public PlayPanel[] arrayPlayBackPanel = new PlayPanel[NETDEMO.PLAYBACK_PANEL_MAX_SIZE];
        
        public PlayPanel m_curPlayBackPanel;
        PlayBackInfo m_playBackInfo = new PlayBackInfo();
        List<NETDEMO.NETDEMO_UPDATE_TIME_INFO> m_downloadInfoList = new List<NETDEMO.NETDEMO_UPDATE_TIME_INFO>();
        DownloadInfo m_downloadInfo = new DownloadInfo();

        public CycleMonitorInfo m_cycleMonitorInfo = null;
        private Thread m_cycleMonitorThread = null;
        private Thread m_keepAliveDeviceThread = null;

        /*callback function*/
        NETDEVSDK.NETDEV_AlarmMessCallBack_PF alarmCB = null;
        NETDEVSDK.NETDEV_AlarmMessCallBack_PF_V30 alarmCBV30 = null;
        NETDEVSDK.NETDEV_ExceptionCallBack_PF excepCB = null;
        NETDEVSDK.NETDEV_FaceSnapshotCallBack_PF faceSnapCB = null;
        public NETDEVSDK.NETDEV_DISCOVERY_CALLBACK_PF discoveryCB = null;
        NETDEVSDK.NETDEV_PassengerFlowStatisticCallBack_PF passengerCB = null;

        Config m_config = null;

        /* privacy Mask Cfg*/
        String strSubItemName = "";
        int iItemIndex = -1;

        /******************* Define member variables end *******************************/

        public NetDemo()
        {
            Control.CheckForIllegalCrossThreadCalls = false;
            InitializeComponent();
            InitPanel();
            InitNetDemo();
        }

        //init panel
        private void InitPanel()
        {
            
            for (int i = 0; i < NETDEMO.REAL_PANEL_MAX_SIZE; i++)
            {
                arrayRealPanel[i] = new PlayPanel();
                arrayRealPanel[i].Padding = new Padding(0);
                arrayRealPanel[i].Margin = new Padding(0);
                arrayRealPanel[i].BackColor = Color.Black;
                arrayRealPanel[i].Name = "realPanel " + i.ToString();
                arrayRealPanel[i].setBorderColor(Color.White, 1);
                arrayRealPanel[i].m_panelIndex = i;
                this.LayoutPanel.Controls.Add(arrayRealPanel[i]);
                arrayRealPanel[i].ContextMenuStrip = this.PannelContextMenuStrip;
                arrayRealPanel[i].Click += new EventHandler(realPanel_Click);
                arrayRealPanel[i].DoubleClick += new EventHandler(realPanel_DoubleClick);
                arrayRealPanel[i].MouseDown += new MouseEventHandler(realPanel_MouseDown);
                arrayRealPanel[i].MouseUp += new MouseEventHandler(realPanel_MouseUp);
                arrayRealPanel[i].MouseMove += new MouseEventHandler(realPanel_MouseMove);
            }
            
            m_curRealPanel = arrayRealPanel[0];
            m_curRealPanel.setBorderColor(Color.Red, 2);
            m_curRealPanel.Invalidate();

            
            for (int i = 0; i < NETDEMO.PLAYBACK_PANEL_MAX_SIZE; i++)
            {
                arrayPlayBackPanel[i] = new PlayPanel();
                arrayPlayBackPanel[i].Padding = new Padding(0);
                arrayPlayBackPanel[i].Margin = new Padding(0);
                arrayPlayBackPanel[i].BackColor = Color.Black;
                arrayPlayBackPanel[i].Name = "playBackPanel " + i.ToString();
                arrayPlayBackPanel[i].m_panelIndex = i;
                arrayPlayBackPanel[i].setBorderColor(Color.White, 1);
                this.playBackLayoutPanel.Controls.Add(arrayPlayBackPanel[i]);
                arrayPlayBackPanel[i].Click += new EventHandler(playBackPanel_Click);
                arrayPlayBackPanel[i].DoubleClick += new EventHandler(playBackPanel_DoubleClick);
            }

            int nSqrt = (int)Math.Sqrt(NETDEMO.PLAYBACK_PANEL_MAX_SIZE);
            int nHeight = this.playBackLayoutPanel.Height / nSqrt - 5;
            int nWidth = this.playBackLayoutPanel.Width / nSqrt - 5;
            for (int i = 0; i < NETDEMO.PLAYBACK_PANEL_MAX_SIZE; i++)
            {
                arrayPlayBackPanel[i].Height = nHeight;
                arrayPlayBackPanel[i].Width = nWidth;
                this.playBackLayoutPanel.Controls.Add(arrayPlayBackPanel[i]);
            }

            m_curPlayBackPanel = arrayPlayBackPanel[0];
            m_curPlayBackPanel.setBorderColor(Color.Red, 2);
            m_curPlayBackPanel.Invalidate();
        }

        //inie demo app
        private void InitNetDemo()
        {
            this.DeviceTree.ExpandAll();

            this.comboBoxMultiScreen.SelectedIndex = 3;//16

            BindDataForm();

            int iRet = NETDEVSDK.NETDEV_Init();
            if (NETDEVSDK.TRUE != iRet)
            {
                MessageBox.Show("it is not a admin oper");
            }

            m_oPtzControl = new PTZControl();
            m_config = new Config(this);

            alarmCB = new NETDEVSDK.NETDEV_AlarmMessCallBack_PF(alarmMessCallBack);
            alarmCBV30 = new NETDEVSDK.NETDEV_AlarmMessCallBack_PF_V30(alarmMessCallBackV30);
            excepCB = new NETDEVSDK.NETDEV_ExceptionCallBack_PF(exceptionCallBack);
            faceSnapCB = new NETDEVSDK.NETDEV_FaceSnapshotCallBack_PF(FaceSnapshotCallBack);

            setSavePath();
            setSaveKeepLiveTime();
            setSaveTimeOut();
            setSaveOperLog();
            startKeepAliveDeviceThread();
            /* Playback module registration timer handler function */
            m_playBackInfo.m_timer.Elapsed += timer_Elapsed;
        }   

        //set save path
        private void setSavePath()
        {
            String m_currentPath = System.Windows.Forms.Application.StartupPath;
            string pictureSavePath = m_currentPath + "\\Pic\\";
            string localRecordPath = m_currentPath + "\\Record\\";
            string logPath = m_currentPath + "\\log\\";

            LocalSetting.setPath(pictureSavePath, localRecordPath, logPath);
        }

        //set save keep live time
        private void setSaveKeepLiveTime()
        {
            Int32 iWaitTime = Convert.ToInt32(15);
            Int32 iTryTime = Convert.ToInt32(3);            
            LocalSetting.setKeepLiveTime(iWaitTime,iTryTime);
        }

        //set save Time Out
        private void setSaveTimeOut()
        {
            Int32 iRevTimeOut = Convert.ToInt32(5);
            Int32 iFileReportTimeOut = Convert.ToInt32(60);
            LocalSetting.setTimeOut(iRevTimeOut, iFileReportTimeOut);
        }

        //set save oper log
        private void setSaveOperLog()
        {
            bool bAutoSaveCkBox = true;
            bool bFailureLogCkBox = true;
            bool bSuccessLogCkBox = true;
            LocalSetting.setOperLog(bAutoSaveCkBox, bFailureLogCkBox, bSuccessLogCkBox);
        }

        private void BindDataForm()
        {
            this.LayoutPanel.Controls.Clear();

            string strScreenNumber = this.comboBoxMultiScreen.SelectedItem.ToString();
            int nScreenNumber = int.Parse(strScreenNumber);
            int nSqrt = (int)Math.Sqrt(nScreenNumber);
            int nHeight = this.LayoutPanel.Height / nSqrt - 5;
            int nWidth = this.LayoutPanel.Width / nSqrt - 5;
            for (int i = 0; i < nScreenNumber; i++)
            {
                arrayRealPanel[i].Height = nHeight;
                arrayRealPanel[i].Width = nWidth;
                this.LayoutPanel.Controls.Add(arrayRealPanel[i]);
            }
        }


        //discovery dialog
        private void Discovery_Click(object sender, EventArgs e)
        {
            if (null == objDiscovery)
            {
                objDiscovery = new Discovery(this);
            }
            
            objDiscovery.ShowDialog();
        }

        // set real panel border color
        private void setRealPanelBorderColor()
        {
            for (int i = 0; i < NETDEMO.REAL_PANEL_MAX_SIZE; i++)
            {
                arrayRealPanel[i].setBorderColor(Color.White, 1);
                arrayRealPanel[i].Invalidate();
            }
            m_curRealPanel.setBorderColor(Color.Red, 2);
            m_curRealPanel.Invalidate();
        }
        
        //live view panel
        private void realPanel_Click(object sender, EventArgs e)
        {
            m_curRealPanel = sender as PlayPanel;
            setDeviceTreeSelectNode();

            if (true == m_curRealPanel.m_recordStatus)
            {
                LocalRecodBtn.Text = "Stop";
            }
            else
            {
                LocalRecodBtn.Text = "Record";
            }

            if (true == m_curRealPanel.m_micStatus)
            {
                MicVolumeBtn.BackgroundImage = global::NetDemo.Properties.Resources.Mic123;
                //MicVolumeBtn.Enabled = true;
                //SliMicVolume.Enabled = true;
                SliMicVolume.Value = m_curRealPanel.m_micVolume;
            }
            else
            {
                MicVolumeBtn.BackgroundImage = global::NetDemo.Properties.Resources._222;
                //MicVolumeBtn.Enabled = false;
                //SliMicVolume.Enabled = false;
                SliMicVolume.Value = 0;
            }

            if (true == m_curRealPanel.m_soundStatus)
            {
                SoundBtn.BackgroundImage = global::NetDemo.Properties.Resources.ico00008;
                //SoundBtn.Enabled = true;
                //SliSoundVolume.Enabled = true;
                SliSoundVolume.Value = m_curRealPanel.m_volume;
            }
            else
            {
                SoundBtn.BackgroundImage = global::NetDemo.Properties.Resources.ico00009;
                //SoundBtn.Enabled = false;
                //SliSoundVolume.Enabled = false;
                SliSoundVolume.Value = 0;
            }

            this.m_curRealPanelIndex = m_curRealPanel.m_panelIndex + 1;
            setRealPanelBorderColor();
            setPTZControlBtnStatus();
        }

        public TreeNode FindNode(TreeNode tnParent, int dwDeviceIndex, int dwID, NETDEMO.NETDEMO_FIND_TREE_NODE_TYPE_E dwFindType)
        {
            if (tnParent == null) return null;
            TreeNodeInfo treeNodeInfo = (TreeNodeInfo)tnParent.Tag;

            if (NETDEMO.NETDEMO_FIND_TREE_NODE_TYPE_E.NETDEMO_FIND_TREE_NODE_CHN_ID == dwFindType)
            {
                if (null != treeNodeInfo && treeNodeInfo.dwDeviceIndex == dwDeviceIndex &&
                treeNodeInfo.dwChannelID == dwID)
                {
                    return tnParent;
                }
            }
            else if (NETDEMO.NETDEMO_FIND_TREE_NODE_TYPE_E.NETDEMO_FIND_TREE_NODE_SUB_DEVICE_ID == dwFindType)
            {
                if (null != treeNodeInfo 
                    && treeNodeInfo.dwDeviceIndex == dwDeviceIndex 
                    && treeNodeInfo.dwSubDeviceID == dwID
                    && (tnParent.ImageIndex == NETDEMO.NETDEV_TREEVIEW_IMAGE_VMS_SUB_DEVICE_ON || tnParent.ImageIndex == NETDEMO.NETDEV_TREEVIEW_IMAGE_VMS_SUB_DEVICE_OFF))
                {
                    return tnParent;
                }
            }
            else if (NETDEMO.NETDEMO_FIND_TREE_NODE_TYPE_E.NETDEMO_FIND_TREE_NODE_ORG_ID == dwFindType)
            {
                if (null != treeNodeInfo
                    && treeNodeInfo.dwDeviceIndex == dwDeviceIndex
                    && treeNodeInfo.dwOrgID == dwID
                    && tnParent.ImageIndex == NETDEMO.NETDEV_TREEVIEW_IMAGE_ORG)
                {
                    return tnParent;
                }
            }
            else if (NETDEMO.NETDEMO_FIND_TREE_NODE_TYPE_E.NETDEMO_FIND_TREE_NODE_DEVICE_INDEX == dwFindType)
            {
                if (null != treeNodeInfo
                    && treeNodeInfo.dwDeviceIndex == dwDeviceIndex
                    && (tnParent.ImageIndex == NETDEMO.NETDEV_TREEVIEW_IMAGE_LOCAL_DEVICE_ON
                        || tnParent.ImageIndex == NETDEMO.NETDEV_TREEVIEW_IMAGE_LOCAL_DEVICE_OFF
                        || tnParent.ImageIndex == NETDEMO.NETDEV_TREEVIEW_IMAGE_CLOUD_DEVICE_ON
                        || tnParent.ImageIndex == NETDEMO.NETDEV_TREEVIEW_IMAGE_CLOUD_DEVICE_OFF))
                {
                    return tnParent;
                }
            }

            TreeNode tnRet = null;
            foreach (TreeNode tn in tnParent.Nodes)
            {
                tnRet = FindNode(tn, dwDeviceIndex, dwID, dwFindType);
                if (tnRet != null) break;
            }
            return tnRet;
        }

        public TreeNode TreeViewFindNode(TreeView tv, int dwDeviceIndex, int dwChannelID, NETDEMO.NETDEMO_FIND_TREE_NODE_TYPE_E dwFindType)
        {
            TreeNode tnRet = null;
            foreach (TreeNode tn in tv.Nodes)
            {
                tnRet = FindNode(tn, dwDeviceIndex, dwChannelID, dwFindType);
                if (tnRet != null)
                    return tnRet;
            }
            return null;
        }

        private void setDeviceTreeSelectNode()
        {
            if (IntPtr.Zero == m_curRealPanel.m_playhandle)
            {
                DeviceTree.SelectedNode = DeviceTree.Nodes[0];
                if (null != (TreeNodeInfo)DeviceTree.SelectedNode.Tag)
                {
                    m_CurSelectTreeNodeInfo = (TreeNodeInfo)DeviceTree.SelectedNode.Tag;
                }
            }
            else
            {
                TreeNode treeNode = TreeViewFindNode(DeviceTree, m_curRealPanel.m_deviceIndex, m_curRealPanel.m_channelID, NETDEMO.NETDEMO_FIND_TREE_NODE_TYPE_E.NETDEMO_FIND_TREE_NODE_CHN_ID);
                if (null != treeNode)
                {
                    DeviceTree.SelectedNode = treeNode;
                    if (null != (TreeNodeInfo)DeviceTree.SelectedNode.Tag)
                    {
                        m_CurSelectTreeNodeInfo = (TreeNodeInfo)DeviceTree.SelectedNode.Tag;
                    }
                }
            }
        }

        
        private void switchRealScreen(PlayPanel curSelectedPanel)
        {
            m_curRealPanel = curSelectedPanel;
            this.LayoutPanel.Controls.Clear();
            if (realMaxFlag == true)
            {
                string strScreenNumber = this.comboBoxMultiScreen.SelectedItem.ToString();
                int nScreenNumber = int.Parse(strScreenNumber);
                int nSqrt = (int)Math.Sqrt(nScreenNumber);
                int nHeight = this.LayoutPanel.Height / nSqrt - 6;
                int nWidth = this.LayoutPanel.Width / nSqrt - 6;
                for (int i = 0; i < nScreenNumber; i++)
                {
                    arrayRealPanel[i].Height = nHeight;
                    arrayRealPanel[i].Width = nWidth;
                    this.LayoutPanel.Controls.Add(arrayRealPanel[i]);
                }
                realMaxFlag = false;
            }
            else
            {
                m_curRealPanel.Height = this.LayoutPanel.Height;
                m_curRealPanel.Width = this.LayoutPanel.Width;
                this.LayoutPanel.Controls.Add(m_curRealPanel);
                realMaxFlag = true;
            }
        }


        private void realPanel_DoubleClick(object sender, EventArgs e)
        {

            switchRealScreen(sender as PlayPanel);
            if (MultiScreen.Checked == false)
            {
                MultiScreen.Checked = true;
            }
            else
            {
                MultiScreen.Checked = false;
            }
            setPTZControlBtnStatus();
        }
  
        private void comboBoxMultiScreen_SelectedIndexChanged(object sender, EventArgs e)
        {
            BindDataForm();
        }

        private void Cloud_Click(object sender, EventArgs e)
        {
            if (null == m_addCloudDevice)
            {
                m_addCloudDevice = new AddCloudDevice(this);
            }

            m_addCloudDevice.ShowDialog();
        }

        private void LocalDevice_Click(object sender, EventArgs e)
        {
            AddDevice addDevice = new AddDevice(this);
            addDevice.ShowDialog();
        }

        private void DeviceTree_MouseUp(object sender, MouseEventArgs e)
        {
            TreeView oTreeView = sender as TreeView;
            if (null == oTreeView)
            {
                return;
            }
            Point oPoint = oTreeView.PointToClient(Cursor.Position);
            TreeViewHitTestInfo info = oTreeView.HitTest(oPoint.X, oPoint.Y);
            TreeNode oNode = info.Node;// 
            if (null == oNode)
            {
                return;
            }
            if (null == oNode.Parent && MouseButtons.Right == e.Button)
            {
                rootOper.Show(MousePosition);
            }
            else if (oNode.Name == "level2" && MouseButtons.Right == e.Button)
            {
                deviceOper.Show(MousePosition);
            }
        }


        private void DeviceTree_Click(object sender, EventArgs e)
        {
            TreeView oTreeView = sender as TreeView;
            if (null == oTreeView)
            {
                return;
            }
            TreeNode selectedNode = oTreeView.SelectedNode;
            if (null != (TreeNodeInfo)selectedNode.Tag)
            {
                m_CurSelectTreeNodeInfo = (TreeNodeInfo)DeviceTree.SelectedNode.Tag;
            }
            
            if (null == selectedNode 
                || selectedNode.ImageIndex == NETDEMO.NETDEV_TREEVIEW_IMAGE_ROOT
                || selectedNode.ImageIndex == NETDEMO.NETDEV_TREEVIEW_IMAGE_ORG)
            {
                return;
            }

            if (selectedNode.ImageIndex == NETDEMO.NETDEV_TREEVIEW_IMAGE_CLOUD_DEVICE_ON ||
                selectedNode.ImageIndex == NETDEMO.NETDEV_TREEVIEW_IMAGE_CLOUD_DEVICE_OFF ||
                selectedNode.ImageIndex == NETDEMO.NETDEV_TREEVIEW_IMAGE_LOCAL_DEVICE_ON ||
                selectedNode.ImageIndex == NETDEMO.NETDEV_TREEVIEW_IMAGE_LOCAL_DEVICE_OFF)
            {
                /** 2017-09-13  start **/
                if (m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_lpDevHandle != IntPtr.Zero)//offline
                {
                    if (this.mainTabCtrl.SelectedTab.Name == "Configure")
                    {
                        m_config.cfgTabSwitch(cfgTabControl.SelectedIndex);
                    }
                }
                
                /** 2017-09-13  end **/
                return;
            }
            else if (selectedNode.ImageIndex == NETDEMO.NETDEV_TREEVIEW_IMAGE_CHL_DEVICE_ON ||
                selectedNode.ImageIndex == NETDEMO.NETDEV_TREEVIEW_IMAGE_CHL_DEVICE_OFF)
            {
                if (m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_lpDevHandle != IntPtr.Zero)//offline
                {
                    if (this.mainTabCtrl.SelectedTab.Name == "Configure")
                    {
                        m_config.cfgTabSwitch(cfgTabControl.SelectedIndex);
                    }
                }

                setPTZControlBtnStatus();
            }
        }

        private void DeviceTree_DoubleClick(object sender, EventArgs e)
        {
            TreeView oTreeView = sender as TreeView;
            if (null == oTreeView)
            {
                return;
            }
            TreeNode selectedNode = oTreeView.SelectedNode;
            if (null != (TreeNodeInfo)selectedNode.Tag)
            {
                m_CurSelectTreeNodeInfo = (TreeNodeInfo)DeviceTree.SelectedNode.Tag;
            }
            
            if (null == selectedNode || 
                selectedNode.ImageIndex == NETDEMO.NETDEV_TREEVIEW_IMAGE_ROOT ||
                selectedNode.ImageIndex == NETDEMO.NETDEV_TREEVIEW_IMAGE_ORG ||
                selectedNode.ImageIndex == NETDEMO.NETDEV_TREEVIEW_IMAGE_VMS_SUB_DEVICE_ON ||
                selectedNode.ImageIndex == NETDEMO.NETDEV_TREEVIEW_IMAGE_VMS_SUB_DEVICE_OFF ||
                selectedNode.ImageIndex == NETDEMO.NETDEV_TREEVIEW_IMAGE_LOCAL_DEVICE_ON ||
                selectedNode.ImageIndex == NETDEMO.NETDEV_TREEVIEW_IMAGE_LOCAL_DEVICE_OFF ||
                selectedNode.ImageIndex == NETDEMO.NETDEV_TREEVIEW_IMAGE_CLOUD_DEVICE_ON ||
                selectedNode.ImageIndex == NETDEMO.NETDEV_TREEVIEW_IMAGE_CLOUD_DEVICE_OFF ||
                selectedNode.ImageIndex == NETDEMO.NETDEV_TREEVIEW_IMAGE_CHL_DEVICE_OFF)
            {
                return;
            }

            if (m_curRealPanel.m_playStatus == false)
            {
                startRealPlay();
            }
            else
            {
                stopRealPlay(m_curRealPanel, true);
                startRealPlay();
            }

            string strScreenNumber = this.comboBoxMultiScreen.SelectedItem.ToString();
            int nScreenNumber = int.Parse(strScreenNumber);

            m_curRealPanelIndex = m_curRealPanel.m_panelIndex + 1;
            if (m_curRealPanelIndex >= nScreenNumber)
            {
                m_curRealPanelIndex = 0;
            }
            
            m_curRealPanel = arrayRealPanel[m_curRealPanelIndex];
            this.setRealPanelBorderColor();
        }

        private void DeviceTree_MouseDown(object sender, MouseEventArgs e)
        {
            TreeView oTreeView = sender as TreeView;
            if (null == oTreeView)
            {
                return;
            }

            oTreeView.SelectedNode = oTreeView.GetNodeAt(e.X, e.Y);
        }

        private void LayoutPanel_MouseUp(object sender, MouseEventArgs e)
        {
            PanelEx oLayoutPanel = sender as PanelEx;
            if (oLayoutPanel == null)
                return;
            for (int i = 0; i < NETDEMO.REAL_PANEL_MAX_SIZE; i++)
            {
                arrayRealPanel[i].setBorderColor(Color.White, 1);
                arrayRealPanel[i].Invalidate();
            }
            oLayoutPanel.setBorderColor(Color.Red, 2);
            oLayoutPanel.Invalidate();
        }

        private void MoreBtn_Click(object sender, EventArgs e)
        {
            if (m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_eDeviceType == NETDEMO.NETDEMO_DEVICE_TYPE_E.NETDEMO_DEVICE_VMS)
            {
                showFailLogInfo(m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_ip + " chl:" + (getChannelID()), "not support", NETDEVSDK.NETDEV_E_NONSUPPORT);
                return;
            }

            if (m_curRealPanel.m_deviceIndex >= m_deviceInfoList.Count || m_curRealPanel.m_deviceIndex < 0)
            {
                return;
            }
            PTZExtend ptzExtend = new PTZExtend(this);
            ptzExtend.Show();
        }

        private void settingLogBtn_Click(object sender, EventArgs e)
        {
            LocalSetting localSetting = new LocalSetting();
            localSetting.ShowDialog();
        }

        private void LocalRecodBtn_Click(object sender, EventArgs e)
        {
            if(m_curRealPanel.m_playStatus == false)
            {
                return;
            }

            if (m_curRealPanel.m_recordStatus == false)
            {
                String temp = string.Copy(LocalSetting.m_strLocalRecordPath);
                DateTime date = DateTime.Now;
                String curTime = date.ToString("yyMMddHHmmss", DateTimeFormatInfo.InvariantInfo);
                LocalSetting.m_strLocalRecordPath += "\\";
                LocalSetting.m_strLocalRecordPath += m_deviceInfoList[m_curRealPanel.m_deviceIndex].m_ip;
                LocalSetting.m_strLocalRecordPath += "_";
                LocalSetting.m_strLocalRecordPath += m_curRealPanel.m_channelID;
                LocalSetting.m_strLocalRecordPath += "_";
                LocalSetting.m_strLocalRecordPath += curTime;

                byte[] localRecordPath;
                GetUTF8Buffer(LocalSetting.m_strLocalRecordPath, NETDEVSDK.NETDEV_LEN_260, out localRecordPath);
                int iRet = NETDEVSDK.NETDEV_SaveRealData(m_curRealPanel.m_playhandle, localRecordPath, (int)NETDEV_MEDIA_FILE_FORMAT_E.NETDEV_MEDIA_FILE_MP4);
                if (NETDEVSDK.FALSE == iRet)
                {
                    showFailLogInfo(m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_ip + " chl:" + (getChannelID()), "start Record", NETDEVSDK.NETDEV_GetLastError());
                    return;
                }
                showSuccessLogInfo(m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_ip + " chl:" + (getChannelID()), "start Record");

                m_curRealPanel.m_recordStatus = true;
                this.LocalRecodBtn.Text = "Stop";
                LocalSetting.m_strLocalRecordPath = temp;
            }
            else
            {
                int iRet = NETDEVSDK.NETDEV_StopSaveRealData(m_curRealPanel.m_playhandle);
                if (NETDEVSDK.FALSE == iRet)
                {
                    showFailLogInfo(m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_ip + " chl:" + (getChannelID()), "stop Record", NETDEVSDK.NETDEV_GetLastError());
                    return;
                }
                showSuccessLogInfo(m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_ip + " chl:" + (getChannelID()), "stop Record");

                m_curRealPanel.m_recordStatus = false;
                this.LocalRecodBtn.Text = "Record";
            }
        }

        
        private void mainTabCtrlSelectedChanged(object sender, EventArgs e)
        {
            if (this.mainTabCtrl.SelectedTab.Name == "LiveView")
            {
                //initLiveViewPage();
            }
            else if (this.mainTabCtrl.SelectedTab.Name == "Playback")
            {
                //initPlaybackPage();
            }
            else if (this.mainTabCtrl.SelectedTab.Name == "Configure")
            {
                if (m_CurSelectTreeNodeInfo.dwDeviceIndex >= 0 && m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_lpDevHandle != IntPtr.Zero)
                {
                    m_config.cfgTabSwitch(0);/*basic0*/
                }
            }
            else if (this.mainTabCtrl.SelectedTab.Name == "AlarmRecords")
            {
                //initAlarmRecordsPage();
            }
            else if (this.mainTabCtrl.SelectedTab.Name == "VCA")
            {
                //initVCAPage();
            }
            else if (this.mainTabCtrl.SelectedTab.Name == "Maintenance")
            {
                //initMaintenancePage();
            }
        }

        private void updateChnTreeNode(TreeNode orgTreeNode, NETDEV_DEV_CHN_ENCODE_INFO_S stChnInfo)
        {
            TreeNodeInfo treeOrgNodeInfo = (TreeNodeInfo)orgTreeNode.Tag;

            TreeNode treeNodeChn = null;
            if (-1 != treeOrgNodeInfo.dwChannelID) //已存在
            {
                treeNodeChn = orgTreeNode;
            }
            else
            {
                stChnInfo.stChnBaseInfo.szChnName += '\0';
                byte[] szDevNametmp = System.Text.Encoding.Default.GetBytes(stChnInfo.stChnBaseInfo.szChnName);
                stChnInfo.stChnBaseInfo.szChnName = GetDefaultString(szDevNametmp);

                treeNodeChn = new TreeNode(stChnInfo.stChnBaseInfo.szChnName);
            }

            if (NETDEV_CHN_STATUS_E.NETDEV_CHN_STATUS_ONLINE == (NETDEV_CHN_STATUS_E)stChnInfo.stChnBaseInfo.dwChnStatus)
            {
                treeNodeChn.ImageIndex = NETDEMO.NETDEV_TREEVIEW_IMAGE_CHL_DEVICE_ON;
                treeNodeChn.SelectedImageIndex = NETDEMO.NETDEV_TREEVIEW_IMAGE_CHL_DEVICE_ON;
            }
            else
            {
                treeNodeChn.ImageIndex = NETDEMO.NETDEV_TREEVIEW_IMAGE_CHL_DEVICE_OFF;
                treeNodeChn.SelectedImageIndex = NETDEMO.NETDEV_TREEVIEW_IMAGE_CHL_DEVICE_OFF;
            }

            if (-1 != treeOrgNodeInfo.dwChannelID) //已存在
            {
                Invoke((EventHandler)delegate { orgTreeNode.Text = stChnInfo.stChnBaseInfo.szChnName; });
            }
            else
            {
                TreeNodeInfo treeNodeInfoChn = new TreeNodeInfo();
                treeNodeInfoChn.dwDeviceIndex = treeOrgNodeInfo.dwDeviceIndex;
                treeNodeInfoChn.dwOrgID = stChnInfo.stChnBaseInfo.dwOrgID;
                treeNodeInfoChn.dwSubDeviceID = stChnInfo.stChnBaseInfo.dwDevID;
                treeNodeInfoChn.dwChannelID = stChnInfo.stChnBaseInfo.dwChannelID;
                treeNodeChn.Tag = treeNodeInfoChn;

                Invoke((EventHandler)delegate { orgTreeNode.Nodes.Add(treeNodeChn); });
            }
        }

        private void updateSubDeviceTreeNode(TreeNode orgTreeNode, NETDEMO_VMS_DEV_BASIC_INFO_S stVmsSubDevInfo)
        {
            TreeNodeInfo treeOrgNodeInfo = (TreeNodeInfo)orgTreeNode.Tag;
            TreeNode treeNodeDev = null;
            if (-1 != treeOrgNodeInfo.dwSubDeviceID)//已存在
            {
                treeNodeDev = orgTreeNode;
            }
            else
            {
                stVmsSubDevInfo.stDevBasicInfo.szDevName += '\0';
                byte[] szDevNametmp = System.Text.Encoding.Default.GetBytes(stVmsSubDevInfo.stDevBasicInfo.szDevName);
                stVmsSubDevInfo.stDevBasicInfo.szDevName = GetDefaultString(szDevNametmp);

                treeNodeDev = new TreeNode(stVmsSubDevInfo.stDevBasicInfo.szDevName);
                TreeNodeInfo treeNodeInfo = new TreeNodeInfo();
                treeNodeInfo.dwDeviceIndex = treeOrgNodeInfo.dwDeviceIndex;
                treeNodeInfo.dwOrgID = stVmsSubDevInfo.stDevBasicInfo.dwOrgID;
                treeNodeInfo.dwSubDeviceID = stVmsSubDevInfo.stDevBasicInfo.dwDevID;
                treeNodeDev.Tag = treeNodeInfo;
            }

            /* 无论是新增还是修改，均删除子设备下所有通道 */
            Invoke((EventHandler)delegate { treeNodeDev.Nodes.Clear(); }); 

            if (NETDEV_DEVICE_STATUS_E.NETDEV_DEV_STATUS_ONLINE == (NETDEV_DEVICE_STATUS_E)stVmsSubDevInfo.stDevBasicInfo.dwDevStatus)
            {
                treeNodeDev.ImageIndex = NETDEMO.NETDEV_TREEVIEW_IMAGE_VMS_SUB_DEVICE_ON;
                treeNodeDev.SelectedImageIndex = NETDEMO.NETDEV_TREEVIEW_IMAGE_VMS_SUB_DEVICE_ON;

                for (int n = 0; n < stVmsSubDevInfo.stChnInfoList.Count; n++)
                {
                    updateChnTreeNode(treeNodeDev, stVmsSubDevInfo.stChnInfoList[n].stChnInfo);
                }
            }
            else
            {
                treeNodeDev.ImageIndex = NETDEMO.NETDEV_TREEVIEW_IMAGE_VMS_SUB_DEVICE_OFF;
                treeNodeDev.SelectedImageIndex = NETDEMO.NETDEV_TREEVIEW_IMAGE_VMS_SUB_DEVICE_OFF;
            }

            if (-1 != treeOrgNodeInfo.dwSubDeviceID)//已存在
            {

                Invoke((EventHandler)delegate { treeNodeDev.Text = stVmsSubDevInfo.stDevBasicInfo.szDevName; }); 
            }
            else
            {
                Invoke((EventHandler)delegate { orgTreeNode.Nodes.Add(treeNodeDev); }); 
            }
        }

        private void deleteDeviceTreeNode(int treeNodeIndex)
        {
            TreeNode treeNode = TreeViewFindNode(DeviceTree, treeNodeIndex, 0, NETDEMO.NETDEMO_FIND_TREE_NODE_TYPE_E.NETDEMO_FIND_TREE_NODE_DEVICE_INDEX);
            if (null != treeNode)
            {
                treeNode.Nodes.Clear();

                if (treeNode.ImageIndex == NETDEMO.NETDEV_TREEVIEW_IMAGE_LOCAL_DEVICE_ON)
                {
                    treeNode.ImageIndex = NETDEMO.NETDEV_TREEVIEW_IMAGE_LOCAL_DEVICE_OFF;
                    treeNode.SelectedImageIndex = NETDEMO.NETDEV_TREEVIEW_IMAGE_LOCAL_DEVICE_OFF;
                }
                else if (treeNode.ImageIndex == NETDEMO.NETDEV_TREEVIEW_IMAGE_CLOUD_DEVICE_ON)
                {
                    treeNode.ImageIndex = NETDEMO.NETDEV_TREEVIEW_IMAGE_CLOUD_DEVICE_OFF;
                    treeNode.SelectedImageIndex = NETDEMO.NETDEV_TREEVIEW_IMAGE_CLOUD_DEVICE_OFF;
                }
            }
        }

        private void setDeviceTreeNode(TreeNode deviceTreeNode, int DeviceIndex, DeviceInfo deviceInfoTemp)
        {
            if (deviceInfoTemp.m_lpCloudDevHandle == IntPtr.Zero)
            {
                deviceTreeNode.ImageIndex = NETDEMO.NETDEV_TREEVIEW_IMAGE_LOCAL_DEVICE_ON;
                deviceTreeNode.SelectedImageIndex = NETDEMO.NETDEV_TREEVIEW_IMAGE_LOCAL_DEVICE_ON;
            }
            else
            {
                deviceTreeNode.ImageIndex = NETDEMO.NETDEV_TREEVIEW_IMAGE_CLOUD_DEVICE_ON;
                deviceTreeNode.SelectedImageIndex = NETDEMO.NETDEV_TREEVIEW_IMAGE_CLOUD_DEVICE_ON;
            }

            if (NETDEMO.NETDEMO_DEVICE_TYPE_E.NETDEMO_DEVICE_VMS == deviceInfoTemp.m_eDeviceType)
            {
                TreeNode treeNodeOrgRoot = null;
                TreeNode[] treeNodeOrg = new TreeNode[deviceInfoTemp.stVmsDevInfo.stOrgInfoList.Count];
                for (int i = 0; i < deviceInfoTemp.stVmsDevInfo.stOrgInfoList.Count; i++)
                {
                    deviceInfoTemp.stVmsDevInfo.stOrgInfoList[i].stOrgInfo.szNodeName += '\0';
                    byte[] szDevNametmp = System.Text.Encoding.Default.GetBytes(deviceInfoTemp.stVmsDevInfo.stOrgInfoList[i].stOrgInfo.szNodeName);
                    deviceInfoTemp.stVmsDevInfo.stOrgInfoList[i].stOrgInfo.szNodeName = GetDefaultString(szDevNametmp);

                    treeNodeOrg[i] = new TreeNode(deviceInfoTemp.stVmsDevInfo.stOrgInfoList[i].stOrgInfo.szNodeName);
                    treeNodeOrg[i].ImageIndex = NETDEMO.NETDEV_TREEVIEW_IMAGE_ORG;
                    treeNodeOrg[i].SelectedImageIndex = NETDEMO.NETDEV_TREEVIEW_IMAGE_ORG;

                    TreeNodeInfo treeNodeInfo = new TreeNodeInfo();
                    treeNodeInfo.dwOrgID = deviceInfoTemp.stVmsDevInfo.stOrgInfoList[i].stOrgInfo.dwOrgID;
                    treeNodeInfo.dwDeviceIndex = DeviceIndex;
                    treeNodeOrg[i].Tag = treeNodeInfo;
                }

                for (int i = 0; i < deviceInfoTemp.stVmsDevInfo.stOrgInfoList.Count; i++)
                {
                    for (int j = 0; j < deviceInfoTemp.stVmsDevInfo.stOrgInfoList.Count; j++)
                    {
                        if (deviceInfoTemp.stVmsDevInfo.stOrgInfoList[i].stOrgInfo.dwOrgID == deviceInfoTemp.stVmsDevInfo.stOrgInfoList[j].stOrgInfo.dwParentID)
                        {
                            treeNodeOrg[i].Nodes.Add(treeNodeOrg[j]);
                        }
                    }

                    if (0 == deviceInfoTemp.stVmsDevInfo.stOrgInfoList[i].stOrgInfo.dwParentID)
                    {
                        treeNodeOrgRoot = treeNodeOrg[i];
                    }

                    /* device and channel */
                    for (int k = 0; k < deviceInfoTemp.stVmsDevInfo.stOrgInfoList[i].stVmsDevBasicInfoList.Count; k++)
                    {
                        updateSubDeviceTreeNode(treeNodeOrg[i], deviceInfoTemp.stVmsDevInfo.stOrgInfoList[i].stVmsDevBasicInfoList[k]);
                    }
                }

                Invoke((EventHandler)delegate { deviceTreeNode.Nodes.Add(treeNodeOrgRoot); }); 
            }
            else
            {
                for (int i = 0; i < deviceInfoTemp.m_channelNumber; i++)
                {

                    TreeNode treeNodeChl = new TreeNode("channel " + Convert.ToString(i + 1));
                    if (deviceInfoTemp.m_channelInfoList[i].m_devVideoChlInfo.enStatus == (int)NETDEV_CHANNEL_STATUS_E.NETDEV_CHL_STATUS_ONLINE)
                    {
                        treeNodeChl.ImageIndex = NETDEMO.NETDEV_TREEVIEW_IMAGE_CHL_DEVICE_ON;
                        treeNodeChl.SelectedImageIndex = NETDEMO.NETDEV_TREEVIEW_IMAGE_CHL_DEVICE_ON;
                    }
                    else
                    {
                        treeNodeChl.ImageIndex = NETDEMO.NETDEV_TREEVIEW_IMAGE_CHL_DEVICE_OFF;
                        treeNodeChl.SelectedImageIndex = NETDEMO.NETDEV_TREEVIEW_IMAGE_CHL_DEVICE_OFF;
                    }

                    TreeNodeInfo treeNodeInfo = new TreeNodeInfo();
                    treeNodeInfo.dwDeviceIndex = DeviceIndex;
                    treeNodeInfo.dwChannelID = deviceInfoTemp.m_channelInfoList[i].m_devVideoChlInfo.dwChannelID;
                    treeNodeChl.Tag = treeNodeInfo;
                    Invoke((EventHandler)delegate { deviceTreeNode.Nodes.AddRange(new TreeNode[] { treeNodeChl }); }); 
                }
            }
        }
    
        //TreeView
        private void setTreeView(DeviceInfo deviceInfoTemp)
        {
            int DeviceIndex = m_deviceInfoList.Count;
            TreeNode treeNode = null;
            if (deviceInfoTemp.m_lpCloudDevHandle == IntPtr.Zero)
            {
                treeNode = new TreeNode(deviceInfoTemp.m_ip);
            }
            else
            {
                string strDevName = GetDefaultString(deviceInfoTemp.m_stCloudDevInfo.szDevName);
                treeNode = new TreeNode(strDevName);
            }
            treeNode.Name = "level2";
            TreeNodeInfo treeNodeInfoDevice = new TreeNodeInfo();
            treeNodeInfoDevice.dwDeviceIndex = DeviceIndex;
            treeNode.Tag = treeNodeInfoDevice;

            setDeviceTreeNode(treeNode, DeviceIndex, deviceInfoTemp);

            if (null == this.DeviceTree.Nodes)
            {
                return;
            }
            Invoke((EventHandler)delegate { this.DeviceTree.Nodes[0].Nodes.Add(treeNode);}); 
            this.DeviceTree.SelectedNode = DeviceTree.Nodes[0].Nodes[0];//
            m_CurSelectTreeNodeInfo = (TreeNodeInfo)this.DeviceTree.SelectedNode.Tag;
        }

        //add local device
        public void AddLocalDevice(String ipAddr, short port, String userName, String password, NETDEMO.NETDEMO_DEVICE_TYPE_E eDeviceType)
        {
            lock (m_notLoggeddeviceInfoListlocker)
            {
                DeviceInfo deviceInfoTemp = new DeviceInfo();
                deviceInfoTemp.m_ip = ipAddr;
                deviceInfoTemp.m_port = port;
                deviceInfoTemp.m_userName = userName;
                deviceInfoTemp.m_password = password;
                deviceInfoTemp.m_eDeviceType = eDeviceType;

                m_notLoggeddeviceInfoList.Add(deviceInfoTemp);
            }
        }

        //login local device
        public void LoginLocalDevice(DeviceInfo deviceInfo)
        {
            NETDEMO.NETDEV_LOGIN_TYPE_E loginFlag = NETDEMO.NETDEV_LOGIN_TYPE_E.NETDEV_NEW_LOGIN;
            int DeviceNodeIndex = 0;
            for (int i = 0; i < m_deviceInfoList.Count(); i++)
            {
                if (NETDEMO.NETDEMO_DEVICE_TYPE_E.NETDEMO_DEVICE_INVALID == m_deviceInfoList[i].m_eDeviceType)
                {
                    continue;
                }

                if (deviceInfo.m_ip == m_deviceInfoList[i].m_ip 
                    && deviceInfo.m_port == m_deviceInfoList[i].m_port
                    && m_deviceInfoList[i].m_lpDevHandle != IntPtr.Zero)
                {
                    MessageBox.Show("The device already exists!");
                    return;
                }

                if (deviceInfo.m_ip == m_deviceInfoList[i].m_ip
                    && deviceInfo.m_port == m_deviceInfoList[i].m_port 
                    && m_deviceInfoList[i].m_lpDevHandle == IntPtr.Zero)//again login
                {
                    loginFlag = NETDEMO.NETDEV_LOGIN_TYPE_E.NETDEV_AGAIN_LOGIN;
                    DeviceNodeIndex = i;
                }
            }

            NETDEV_DEVICE_LOGIN_INFO_S pstDevLoginInfo = new NETDEV_DEVICE_LOGIN_INFO_S();
            NETDEV_SELOG_INFO_S pstSELogInfo = new NETDEV_SELOG_INFO_S();
            pstDevLoginInfo.szIPAddr = deviceInfo.m_ip;
            pstDevLoginInfo.dwPort = deviceInfo.m_port;
            pstDevLoginInfo.szUserName = deviceInfo.m_userName;
            pstDevLoginInfo.szPassword = deviceInfo.m_password;
            if(NETDEMO.NETDEMO_DEVICE_TYPE_E.NETDEMO_DEVICE_VMS == deviceInfo.m_eDeviceType)
            {
                pstDevLoginInfo.dwLoginProto = (int)NETDEV_LOGIN_PROTO_E.NETDEV_LOGIN_PROTO_PRIVATE;
            }
            else
            {
                pstDevLoginInfo.dwLoginProto = (int)NETDEV_LOGIN_PROTO_E.NETDEV_LOGIN_PROTO_ONVIF;
            }

            IntPtr lpDevHandle = NETDEVSDK.NETDEV_Login_V30(ref pstDevLoginInfo, ref pstSELogInfo);

            if (lpDevHandle == IntPtr.Zero)
            {
                showFailLogInfo(deviceInfo.m_ip + " : " + deviceInfo.m_port, "login", NETDEVSDK.NETDEV_GetLastError());
                return;
            }

            if(loginFlag == NETDEMO.NETDEV_LOGIN_TYPE_E.NETDEV_AGAIN_LOGIN)
            {
                m_deviceInfoList[DeviceNodeIndex].m_lpDevHandle = lpDevHandle;
            }
            showSuccessLogInfo(deviceInfo.m_ip + " : " + deviceInfo.m_port, "login");

            DeviceInfo deviceInfoTemp = new DeviceInfo();
            deviceInfoTemp.m_lpDevHandle = lpDevHandle;
            deviceInfoTemp.m_ip = deviceInfo.m_ip;
            deviceInfoTemp.m_port = deviceInfo.m_port;
            deviceInfoTemp.m_userName = deviceInfo.m_userName;
            deviceInfoTemp.m_password = deviceInfo.m_password;
            deviceInfoTemp.m_eDeviceType = deviceInfo.m_eDeviceType;

            //set alarm exception callback
            int iRet = NETDEVSDK.NETDEV_SetAlarmCallBack_V30(deviceInfoTemp.m_lpDevHandle, alarmCBV30, IntPtr.Zero);
            if (NETDEVSDK.FALSE == iRet)
            {
                showFailLogInfo(deviceInfo.m_ip + " : " + deviceInfo.m_port, "register AlarmCallBack", NETDEVSDK.NETDEV_GetLastError());
                return;
            }

            showSuccessLogInfo(deviceInfo.m_ip + " : " + deviceInfo.m_port, "register AlarmCallBack");

            iRet = NETDEVSDK.NETDEV_SetExceptionCallBack(excepCB, IntPtr.Zero);
            if (NETDEVSDK.FALSE == iRet)
            {
                showFailLogInfo(deviceInfo.m_ip + " : " + deviceInfo.m_port, "register ExceptionCallBack", NETDEVSDK.NETDEV_GetLastError());
                return;
            }

            showSuccessLogInfo(deviceInfo.m_ip + " : " + deviceInfo.m_port, "register ExceptionCallBack");

            iRet = NETDEVSDK.NETDEV_SetFaceSnapshotCallBack(deviceInfoTemp.m_lpDevHandle, faceSnapCB, IntPtr.Zero);
            if (NETDEVSDK.FALSE == iRet)
            {
                showFailLogInfo(deviceInfo.m_ip + " : " + deviceInfo.m_port, "register FaceSnapshotCallBack", NETDEVSDK.NETDEV_GetLastError());
                return;
            }

            showSuccessLogInfo(deviceInfo.m_ip + " : " + deviceInfo.m_port, "register FaceSnapshotCallBack");

            //get the channel list
            if (NETDEMO.NETDEMO_DEVICE_TYPE_E.NETDEMO_DEVICE_VMS == deviceInfo.m_eDeviceType)
            {
                deviceInfoTemp.stVmsDevInfo = new NETDEMO_VMS_DEVICE_INFO_S();

                NETDEV_ORG_FIND_COND_S stFindCond = new NETDEV_ORG_FIND_COND_S();
                stFindCond.udwRootOrgID = 0;
                IntPtr lpFindOrgHandle = NETDEVSDK.NETDEV_FindOrgInfoList(lpDevHandle, ref stFindCond);
                if (IntPtr.Zero == lpFindOrgHandle)
                {
                    showFailLogInfo(deviceInfo.m_ip + " : " + deviceInfo.m_port, "find org list", NETDEVSDK.NETDEV_GetLastError());
                    return;
                }

                while (true)
                {
                    NETDEMO_VMS_ORG_INFO_S stDemoOrgInfo = new NETDEMO_VMS_ORG_INFO_S();
                    NETDEV_ORG_INFO_S stOrgInfo = new NETDEV_ORG_INFO_S();
                    iRet = NETDEVSDK.NETDEV_FindNextOrgInfo(lpFindOrgHandle, ref stOrgInfo);
                    if (NETDEVSDK.FALSE == iRet)
                    {
                        break;
                    }

                    stDemoOrgInfo.stOrgInfo = stOrgInfo;
                    deviceInfoTemp.stVmsDevInfo.stOrgInfoList.Add(stDemoOrgInfo);
                }

                NETDEVSDK.NETDEV_FindCloseOrgInfo(lpFindOrgHandle);

                IntPtr lpFindDevHandle = NETDEVSDK.NETDEV_FindDevList(lpDevHandle, (int)NETDEV_DEVICE_MAIN_TYPE_E.NETDEV_DTYPE_MAIN_ENCODE);
                if (IntPtr.Zero == lpFindDevHandle)
                {
                    showFailLogInfo(deviceInfo.m_ip + " : " + deviceInfo.m_port, "NETDEV_FindDevList", NETDEVSDK.NETDEV_GetLastError());
                    //return;
                }

                while (true)
                {
                    NETDEMO_VMS_DEV_BASIC_INFO_S stDemoVmsBasicInfo = new NETDEMO_VMS_DEV_BASIC_INFO_S();

                    NETDEV_DEV_BASIC_INFO_S pstDevBasicInfo = new NETDEV_DEV_BASIC_INFO_S();
                    iRet = NETDEVSDK.NETDEV_FindNextDevInfo(lpFindDevHandle, ref pstDevBasicInfo);
                    if (NETDEVSDK.FALSE == iRet)
                    {
                        break;
                    }

                    stDemoVmsBasicInfo.stDevBasicInfo = pstDevBasicInfo;

                    IntPtr lpFindDevChnHandle = NETDEVSDK.NETDEV_FindDevChnList(lpDevHandle, pstDevBasicInfo.dwDevID, 0);
                    if (IntPtr.Zero == lpFindDevChnHandle)
                    {
                        showFailLogInfo(deviceInfo.m_ip + " : " + deviceInfo.m_port, "NETDEV_FindDevChnList", NETDEVSDK.NETDEV_GetLastError());
                        break;
                    }

                    while (true)
                    {
                        NETDEMO_VMS_DEV_CHANNEL_INFO_S stDemoVmsChnInfo = new NETDEMO_VMS_DEV_CHANNEL_INFO_S();

                        int pdwBytesReturned = 0;
                        NETDEV_DEV_CHN_ENCODE_INFO_S stDevChnInfo = new NETDEV_DEV_CHN_ENCODE_INFO_S();
                        IntPtr lpOutBuffer = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(NETDEV_DEV_CHN_ENCODE_INFO_S)));
                        Marshal.StructureToPtr(stDevChnInfo, lpOutBuffer, true);
                        iRet = NETDEVSDK.NETDEV_FindNextDevChn(lpFindDevChnHandle, lpOutBuffer, Marshal.SizeOf(typeof(NETDEV_DEV_CHN_ENCODE_INFO_S)), ref pdwBytesReturned);
                        if (NETDEVSDK.FALSE == iRet)
                        {
                            Marshal.FreeHGlobal(lpOutBuffer);
                            break;
                        }
                        else
                        {
                            stDevChnInfo = (NETDEV_DEV_CHN_ENCODE_INFO_S)Marshal.PtrToStructure(lpOutBuffer, typeof(NETDEV_DEV_CHN_ENCODE_INFO_S));
                            stDemoVmsChnInfo.stChnInfo = stDevChnInfo;
                            stDemoVmsBasicInfo.stChnInfoList.Add(stDemoVmsChnInfo);
                            
                            Marshal.FreeHGlobal(lpOutBuffer);
                        }


                    }

                    NETDEVSDK.NETDEV_FindCloseDevChn(lpFindDevChnHandle);

                    for (int k = 0; k < deviceInfoTemp.stVmsDevInfo.stOrgInfoList.Count; k++)
                    {
                        if (stDemoVmsBasicInfo.stDevBasicInfo.dwOrgID == deviceInfoTemp.stVmsDevInfo.stOrgInfoList[k].stOrgInfo.dwOrgID)
                        {
                            deviceInfoTemp.stVmsDevInfo.stOrgInfoList[k].stVmsDevBasicInfoList.Add(stDemoVmsBasicInfo);
                        }
                    }
                }

                NETDEVSDK.NETDEV_FindCloseDevInfo(lpFindDevHandle);

                if (loginFlag == NETDEMO.NETDEV_LOGIN_TYPE_E.NETDEV_AGAIN_LOGIN)//again login
                {
                    m_deviceInfoList[DeviceNodeIndex] = deviceInfoTemp;
                    TreeNode treeNode = TreeViewFindNode(DeviceTree, DeviceNodeIndex, 0, NETDEMO.NETDEMO_FIND_TREE_NODE_TYPE_E.NETDEMO_FIND_TREE_NODE_DEVICE_INDEX);
                    if (null != treeNode)
                    {
                        setDeviceTreeNode(treeNode, DeviceNodeIndex, deviceInfoTemp);
                        //updatedeviceTreeStatus(DeviceNodeIndex);

                        for (int j = 0; j < deviceInfo.m_RealPlayInfoList.Count; j++)
                        {
                            m_CurSelectTreeNodeInfo = new TreeNodeInfo();
                            m_CurSelectTreeNodeInfo.dwDeviceIndex = DeviceNodeIndex;
                            m_CurSelectTreeNodeInfo.dwChannelID = deviceInfo.m_RealPlayInfoList[j].m_channel;
                            m_curRealPanel = arrayRealPanel[deviceInfo.m_RealPlayInfoList[j].m_panelIndex];
                            startRealPlay();
                        }
                    }
                }
                else
                {
                    setTreeView(deviceInfoTemp);
                    m_deviceInfoList.Add(deviceInfoTemp);
                }
            }
            else
            {
                int pdwChlCount = 256;
                IntPtr pstVideoChlList = new IntPtr();
                //pstVideoChlList = Marshal.AllocHGlobal(NETDEVSDK.NETDEV_LEN_32 * Marshal.SizeOf(typeof(NETDEV_VIDEO_CHL_DETAIL_INFO_S)));
                pstVideoChlList = Marshal.AllocHGlobal(256 * Marshal.SizeOf(typeof(NETDEV_VIDEO_CHL_DETAIL_INFO_S)));
                iRet = NETDEVSDK.NETDEV_QueryVideoChlDetailList(deviceInfoTemp.m_lpDevHandle, ref pdwChlCount, pstVideoChlList);
                if (NETDEVSDK.TRUE == iRet)
                {
                    deviceInfoTemp.m_channelNumber = pdwChlCount;
                    NETDEV_VIDEO_CHL_DETAIL_INFO_S stCHLItem = new NETDEV_VIDEO_CHL_DETAIL_INFO_S();
                    for (int i = 0; i < pdwChlCount; i++)
                    {
                        IntPtr ptrTemp = new IntPtr(pstVideoChlList.ToInt64() + Marshal.SizeOf(typeof(NETDEV_VIDEO_CHL_DETAIL_INFO_S)) * i);
                        stCHLItem = (NETDEV_VIDEO_CHL_DETAIL_INFO_S)Marshal.PtrToStructure(ptrTemp, typeof(NETDEV_VIDEO_CHL_DETAIL_INFO_S));

                        ChannelInfo channelInfo = new ChannelInfo();
                        channelInfo.m_devVideoChlInfo = stCHLItem;
                        deviceInfoTemp.m_channelInfoList.Add(channelInfo);
                    }
                    if (loginFlag == NETDEMO.NETDEV_LOGIN_TYPE_E.NETDEV_AGAIN_LOGIN)//again login
                    {
                        m_deviceInfoList[DeviceNodeIndex] = deviceInfoTemp;
                        TreeNode treeNode = TreeViewFindNode(DeviceTree, DeviceNodeIndex, 0, NETDEMO.NETDEMO_FIND_TREE_NODE_TYPE_E.NETDEMO_FIND_TREE_NODE_DEVICE_INDEX);
                        if (null != treeNode)
                        {
                            setDeviceTreeNode(treeNode, DeviceNodeIndex, deviceInfoTemp);
                            //updatedeviceTreeStatus(DeviceNodeIndex);

                            for (int j = 0; j < deviceInfo.m_RealPlayInfoList.Count; j++)
                            {
                                m_CurSelectTreeNodeInfo = new TreeNodeInfo();
                                m_CurSelectTreeNodeInfo.dwDeviceIndex = DeviceNodeIndex;
                                m_CurSelectTreeNodeInfo.dwChannelID = deviceInfo.m_RealPlayInfoList[j].m_channel;
                                m_curRealPanel = arrayRealPanel[deviceInfo.m_RealPlayInfoList[j].m_panelIndex];
                                startRealPlay();
                            }
                        }
                    }
                    else
                    {
                        setTreeView(deviceInfoTemp);
                        m_deviceInfoList.Add(deviceInfoTemp);
                    }
                }
                Marshal.FreeHGlobal(pstVideoChlList);

                NETDEV_DEVICE_INFO_S pstDevInfo = new NETDEV_DEVICE_INFO_S();
                NETDEVSDK.NETDEV_GetDeviceInfo(deviceInfoTemp.m_lpDevHandle, ref pstDevInfo);
                deviceInfoTemp.m_stDevInfo = pstDevInfo;
            }
        }

        //Add Cloud Device
        public void AddCloudDevice(String url, String userName, String password)
        {
            if (lpCloudDevHandle == IntPtr.Zero)
            {
                lpCloudDevHandle = NETDEVSDK.NETDEV_LoginCloud(url, userName, password);
                if (lpCloudDevHandle == IntPtr.Zero)
                {
                    showFailLogInfo(url, "login cloud account", NETDEVSDK.NETDEV_GetLastError());
                    return;
                }

                showSuccessLogInfo(url, "login cloud account");
            }

            IntPtr lpDevListHandle = NETDEVSDK.NETDEV_FindCloudDevListEx(lpCloudDevHandle);
            if (lpDevListHandle == IntPtr.Zero)
            {
                showFailLogInfo(url, "Query cloud device list", NETDEVSDK.NETDEV_GetLastError());
                return;
            }

            NETDEV_CLOUD_DEV_BASIC_INFO_S stCloudDevInfo;
            
            lock (m_notLoggeddeviceInfoListlocker)  
            {
                int bRet = NETDEVSDK.TRUE;
                while (NETDEVSDK.TRUE == bRet)
                {
                    stCloudDevInfo = new NETDEV_CLOUD_DEV_BASIC_INFO_S();
                    bRet = NETDEVSDK.NETDEV_FindNextCloudDevInfoEx(lpDevListHandle, ref stCloudDevInfo);
                    if(NETDEVSDK.TRUE == bRet)
					{
						DeviceInfo deviceInfoTemp = new DeviceInfo();
						deviceInfoTemp.m_lpCloudDevHandle = lpCloudDevHandle;
						deviceInfoTemp.m_cloudUrl = url;
						deviceInfoTemp.m_cloudUserName = userName;
						deviceInfoTemp.m_cloudPassword = password;
						deviceInfoTemp.m_stCloudDevInfo = stCloudDevInfo;
						deviceInfoTemp.m_ip = deviceInfoTemp.m_stCloudDevInfo.szIPAddr;
						m_notLoggeddeviceInfoList.Add(deviceInfoTemp);
					}
                }
                NETDEVSDK.NETDEV_FindCloseCloudDevListEx(lpDevListHandle);
            }
        }

        //login cloud device
        public void loginCloudDevice(DeviceInfo deviceInfo)
        {
            NETDEMO.NETDEV_LOGIN_TYPE_E loginFlag = NETDEMO.NETDEV_LOGIN_TYPE_E.NETDEV_NEW_LOGIN;

            if (lpCloudDevHandle == IntPtr.Zero)
            {
                return;
            }

            int DeviceNodeIndex = 0;
            for (int i = 0; i < m_deviceInfoList.Count; i++)
            {
                if (NETDEMO.NETDEMO_DEVICE_TYPE_E.NETDEMO_DEVICE_INVALID == m_deviceInfoList[i].m_eDeviceType)
                {
                    continue;
                }
                //cloud device
                if (IntPtr.Zero != m_deviceInfoList[i].m_lpCloudDevHandle)
                {
                    if (deviceInfo.m_stCloudDevInfo.szDevUserName == m_deviceInfoList[i].m_stCloudDevInfo.szDevUserName)
                    {
                        if (IntPtr.Zero == m_deviceInfoList[i].m_lpDevHandle)
                        {
                            loginFlag = NETDEMO.NETDEV_LOGIN_TYPE_E.NETDEV_AGAIN_LOGIN;
                            DeviceNodeIndex = i;
                        }
                        else
                        {
                            return;
                        }
                    }
                    else
                    {
                        continue;
                    }
                }
                else
                {
                    continue;
                }
            }

            DeviceInfo deviceInfoTemp = new DeviceInfo();
            deviceInfoTemp.m_lpCloudDevHandle = lpCloudDevHandle;
            deviceInfoTemp.m_cloudUrl = deviceInfo.m_cloudUrl;
            deviceInfoTemp.m_cloudUserName = deviceInfo.m_cloudUserName;
            deviceInfoTemp.m_cloudPassword = deviceInfo.m_cloudPassword;
            deviceInfoTemp.m_stCloudDevInfo = deviceInfo.m_stCloudDevInfo;
            deviceInfoTemp.m_ip = deviceInfoTemp.m_stCloudDevInfo.szIPAddr;

            NETDEV_CLOUD_DEV_LOGIN_INFO_S pCloudInfo = new NETDEV_CLOUD_DEV_LOGIN_INFO_S();
            pCloudInfo.dwLoginProto = (int)NETDEV_LOGIN_PROTO_E.NETDEV_LOGIN_PROTO_ONVIF;
            pCloudInfo.szDeviceName = deviceInfo.m_stCloudDevInfo.szDevUserName;
            deviceInfoTemp.m_lpDevHandle = NETDEVSDK.NETDEV_LoginCloudDevice_V30(deviceInfoTemp.m_lpCloudDevHandle, ref pCloudInfo);
            if (deviceInfoTemp.m_lpDevHandle == IntPtr.Zero)
            {
                showFailLogInfo(deviceInfo.m_cloudUrl + " : " + deviceInfo.m_stCloudDevInfo.szDevUserName, "Login cloud device", NETDEVSDK.NETDEV_GetLastError());
                return;
            }
            showSuccessLogInfo(deviceInfo.m_cloudUrl + " : " + deviceInfo.m_stCloudDevInfo.szDevUserName, "Login cloud device");

            if (loginFlag == NETDEMO.NETDEV_LOGIN_TYPE_E.NETDEV_AGAIN_LOGIN)
            {
                m_deviceInfoList[DeviceNodeIndex].m_lpDevHandle = deviceInfoTemp.m_lpDevHandle;
            }

            //set alarmexception callback
            //NETDEVSDK.NETDEV_AlarmMessCallBack_PF alarmCB = new NETDEVSDK.NETDEV_AlarmMessCallBack_PF(alarmMessCallBack);
            NETDEVSDK.NETDEV_SetAlarmCallBack(deviceInfoTemp.m_lpDevHandle, alarmCB, IntPtr.Zero);

            //NETDEVSDK.NETDEV_ExceptionCallBack_PF excepCB = new NETDEVSDK.NETDEV_ExceptionCallBack_PF(exceptionCallBack);
            NETDEVSDK.NETDEV_SetExceptionCallBack(excepCB, IntPtr.Zero);

            //get the channel list

            int pdwChlCount = NETDEVSDK.NETDEV_LEN_32;

            IntPtr pstVideoChlList = new IntPtr();
            pstVideoChlList = Marshal.AllocHGlobal(NETDEVSDK.NETDEV_LEN_32 * Marshal.SizeOf(typeof(NETDEV_VIDEO_CHL_DETAIL_INFO_S)));
            int iRet = NETDEVSDK.NETDEV_QueryVideoChlDetailList(deviceInfoTemp.m_lpDevHandle, ref pdwChlCount, pstVideoChlList);
            if (NETDEVSDK.TRUE == iRet)
            {
                deviceInfoTemp.m_channelNumber = pdwChlCount;
                NETDEV_VIDEO_CHL_DETAIL_INFO_S stCHLItem = new NETDEV_VIDEO_CHL_DETAIL_INFO_S();
                for (int i = 0; i < pdwChlCount; i++)
                {
                    IntPtr ptrTemp = new IntPtr(pstVideoChlList.ToInt64() + Marshal.SizeOf(typeof(NETDEV_VIDEO_CHL_DETAIL_INFO_S)) * i);
                    stCHLItem = (NETDEV_VIDEO_CHL_DETAIL_INFO_S)Marshal.PtrToStructure(ptrTemp, typeof(NETDEV_VIDEO_CHL_DETAIL_INFO_S));

                    ChannelInfo channelInfo = new ChannelInfo();
                    channelInfo.m_devVideoChlInfo = stCHLItem;

                    deviceInfoTemp.m_channelInfoList.Add(channelInfo);
                }

                if (loginFlag == NETDEMO.NETDEV_LOGIN_TYPE_E.NETDEV_AGAIN_LOGIN)//again login
                {
                    m_deviceInfoList[DeviceNodeIndex] = deviceInfoTemp;
                    TreeNode treeNode = TreeViewFindNode(DeviceTree, DeviceNodeIndex, 0, NETDEMO.NETDEMO_FIND_TREE_NODE_TYPE_E.NETDEMO_FIND_TREE_NODE_DEVICE_INDEX);
                    if (null != treeNode)
                    {
                        setDeviceTreeNode(treeNode, DeviceNodeIndex, deviceInfoTemp);
                        //updatedeviceTreeStatus(DeviceNodeIndex);

                        for (int j = 0; j < deviceInfo.m_RealPlayInfoList.Count; j++)
                        {
                            m_CurSelectTreeNodeInfo = new TreeNodeInfo();
                            m_CurSelectTreeNodeInfo.dwDeviceIndex = DeviceNodeIndex;
                            m_CurSelectTreeNodeInfo.dwChannelID = deviceInfo.m_RealPlayInfoList[j].m_channel;
                            m_curRealPanel = arrayRealPanel[deviceInfo.m_RealPlayInfoList[j].m_panelIndex];
                            startRealPlay();
                        }
                    }
                }
                else
                {
                    setTreeView(deviceInfoTemp);
                    m_deviceInfoList.Add(deviceInfoTemp);
                }
            }
            else
            {
                /* channel list fail */
                NETDEVSDK.NETDEV_Logout(deviceInfoTemp.m_lpDevHandle);
            }
           
        }


        /************************realPlay control start ************************************/
        //realplay
        private void RealPlay_Click(object sender, EventArgs e)
        {
            if (DeviceTree.SelectedNode == null)
            {
                return;
            }
            if (DeviceTree.SelectedNode.ImageIndex != NETDEMO.NETDEV_TREEVIEW_IMAGE_CHL_DEVICE_ON)
            {
                return;
            }
            if (m_curRealPanel.m_playStatus == false)
            {
                startRealPlay();
            }
            else
            {
                stopRealPlay(m_curRealPanel, true);
                startRealPlay();
            }
        }

        private void Sequence_Click(object sender, EventArgs e)
        {
            CycleMonitor cycleMonitor = new CycleMonitor(this);
            cycleMonitor.ShowDialog();
        }

        public int getChannelID()
        {
            return m_CurSelectTreeNodeInfo.dwChannelID;
        }

        public int getDeviceIndex()
        {
            return m_CurSelectTreeNodeInfo.dwDeviceIndex;
        }

        public int getOrgIndexByID(Int32 dwDeviceIndex, Int32 dwOrgID)
        {
            if (-1 == dwDeviceIndex)
            {
                return -1;
            }

            if (NETDEMO.NETDEMO_DEVICE_TYPE_E.NETDEMO_DEVICE_VMS == m_deviceInfoList[dwDeviceIndex].m_eDeviceType)
            {
                for (int i = 0; i < m_deviceInfoList[dwDeviceIndex].stVmsDevInfo.stOrgInfoList.Count; i++)
                {
                    if (dwOrgID == m_deviceInfoList[dwDeviceIndex].stVmsDevInfo.stOrgInfoList[i].stOrgInfo.dwOrgID)
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        public int getOrgIndex()
        {
            if (-1 == m_CurSelectTreeNodeInfo.dwDeviceIndex)
            {
                return -1;
            }

            if (NETDEMO.NETDEMO_DEVICE_TYPE_E.NETDEMO_DEVICE_VMS == m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_eDeviceType)
            {
                for (int i = 0; i < m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].stVmsDevInfo.stOrgInfoList.Count; i++)
                {
                    if (m_CurSelectTreeNodeInfo.dwOrgID == m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].stVmsDevInfo.stOrgInfoList[i].stOrgInfo.dwOrgID)
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        public int getSubDeviceIndexByID(Int32 dwDeviceIndex, Int32 dwOrgIndex, Int32 dwSubDevID)
        {
            if (-1 != dwOrgIndex)
            {
                for (int i = 0; i < m_deviceInfoList[dwDeviceIndex].stVmsDevInfo.stOrgInfoList[dwOrgIndex].stVmsDevBasicInfoList.Count; i++)
                {
                    if (dwSubDevID == m_deviceInfoList[dwDeviceIndex].stVmsDevInfo.stOrgInfoList[dwOrgIndex].stVmsDevBasicInfoList[i].stDevBasicInfo.dwDevID)
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        public int getSubDeviceIndex()
        {
            int dwOrgIndex = getOrgIndex();
            if (-1 != dwOrgIndex)
            {
                for (int i = 0; i < m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].stVmsDevInfo.stOrgInfoList[dwOrgIndex].stVmsDevBasicInfoList.Count; i++)
                {
                    if (m_CurSelectTreeNodeInfo.dwSubDeviceID == m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].stVmsDevInfo.stOrgInfoList[dwOrgIndex].stVmsDevBasicInfoList[i].stDevBasicInfo.dwDevID)
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        public int getChannelIndexByID(Int32 dwDeviceIndex, Int32 dwOrgIndex, Int32 dwSubDevIndex, Int32 dwChannelID)
        {
            if (NETDEMO.NETDEMO_DEVICE_TYPE_E.NETDEMO_DEVICE_VMS == m_deviceInfoList[dwDeviceIndex].m_eDeviceType)
            {
                if (-1 != dwOrgIndex && -1 != dwSubDevIndex)
                {
                    for (int i = 0; i < m_deviceInfoList[dwDeviceIndex].stVmsDevInfo.stOrgInfoList[dwOrgIndex].stVmsDevBasicInfoList[dwSubDevIndex].stChnInfoList.Count; i++)
                    {
                        if (dwChannelID == m_deviceInfoList[dwDeviceIndex].stVmsDevInfo.stOrgInfoList[dwOrgIndex].stVmsDevBasicInfoList[dwSubDevIndex].stChnInfoList[i].stChnInfo.stChnBaseInfo.dwChannelID)
                        {
                            return i;
                        }
                    }
                }

                return -1;
            }
            else
            {
                /* NVR和IPC设备本身信息存储在通道1中，所以应该返回通道1的索引下标0 */
                if (-1 == dwChannelID)
                {
                    return 0;
                }

                return dwChannelID - 1;
            }
        }

        public int getChannelIndex()
        {
            if (-1 == m_CurSelectTreeNodeInfo.dwDeviceIndex)
            {
                return -1;
            }

            if (NETDEMO.NETDEMO_DEVICE_TYPE_E.NETDEMO_DEVICE_VMS == m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_eDeviceType)
            {
                int dwOrgIndex = getOrgIndex();
                int dwSubDeviceIndex = getSubDeviceIndex();

                if (-1 != dwOrgIndex && -1 != dwSubDeviceIndex)
                {
                    for (int i = 0; i < m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].stVmsDevInfo.stOrgInfoList[dwOrgIndex].stVmsDevBasicInfoList[dwSubDeviceIndex].stChnInfoList.Count; i++)
                    {
                        if (m_CurSelectTreeNodeInfo.dwChannelID == m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].stVmsDevInfo.stOrgInfoList[dwOrgIndex].stVmsDevBasicInfoList[dwSubDeviceIndex].stChnInfoList[i].stChnInfo.stChnBaseInfo.dwChannelID)
                        {
                            return i;
                        }
                    }
                }

                return -1;
            }
            else
            {
                /* NVR和IPC设备本身信息存储在通道1中，所以应该返回通道1的索引下标0 */
                if (-1 == m_CurSelectTreeNodeInfo.dwChannelID)
                {
                    return 0;
                }

                return m_CurSelectTreeNodeInfo.dwChannelID - 1;
            }
        }
        
        public void startRealPlay()
        {
            if (m_CurSelectTreeNodeInfo.dwDeviceIndex < 0 || getChannelID() < 0)
            {
                return;
            }

            m_curRealPanel.initPlayPanel();
            m_curRealPanel.m_deviceIndex = m_CurSelectTreeNodeInfo.dwDeviceIndex;
            m_curRealPanel.m_channelID = getChannelID();

            NETDEV_PREVIEWINFO_S stPreviewInfo = new NETDEV_PREVIEWINFO_S();
            stPreviewInfo.dwChannelID = getChannelID();
            //stPreviewInfo.dwFluency = 
            stPreviewInfo.dwLinkMode = (int)NETDEV_PROTOCAL_E.NETDEV_TRANSPROTOCAL_RTPTCP;
            stPreviewInfo.dwStreamType = (int)NETDEV_LIVE_STREAM_INDEX_E.NETDEV_LIVE_STREAM_INDEX_MAIN;
            stPreviewInfo.hPlayWnd = m_curRealPanel.Handle;
            IntPtr Handle = NETDEVSDK.NETDEV_RealPlay(m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_lpDevHandle, ref stPreviewInfo, IntPtr.Zero, IntPtr.Zero);
            if (Handle == IntPtr.Zero)
            {
                return;
            }
            m_curRealPanel.m_playStatus = true;
            m_curRealPanel.m_playhandle = Handle;

            RealPlayInfo objRealPlayInfo = new RealPlayInfo();
            objRealPlayInfo.m_channel = getChannelID();
            objRealPlayInfo.m_panelIndex = m_curRealPanel.m_panelIndex;
            m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].addRealPlayInfo(objRealPlayInfo);

            NETDEVSDK.NETDEV_SetIVAEnable(Handle, 1);
            NETDEVSDK.NETDEV_SetIVAShowParam(7);

            realPlayOpenSound();
        }

        private void realPlayOpenSound()
        {
            int iRet = NETDEVSDK.NETDEV_OpenSound(m_curRealPanel.m_playhandle);
            if (NETDEVSDK.TRUE != iRet)
            {
                showFailLogInfo(m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_ip + " chl:" + (getChannelID()), "Open sound", NETDEVSDK.NETDEV_GetLastError());
                return;
            }
            showSuccessLogInfo(m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_ip + " chl:" + (getChannelID()), "Open sound");

            m_curRealPanel.m_soundStatus = true;
            this.SoundBtn.BackgroundImage = global::NetDemo.Properties.Resources.ico00008;
            this.SoundBtn.Enabled = true;

            int volumeTemp = 0;
            iRet = NETDEVSDK.NETDEV_GetSoundVolume(m_curRealPanel.m_playhandle, ref volumeTemp);
            if (NETDEVSDK.FALSE == iRet)
            {
                showFailLogInfo(m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_ip + " chl:" + (getChannelID()), "Get sound volume", NETDEVSDK.NETDEV_GetLastError());
                return;
            }
            showSuccessLogInfo(m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_ip + " chl:" + (getChannelID()), "Get sound volume");

            m_curRealPanel.m_volume = volumeTemp;

            this.SliSoundVolume.Value = m_curRealPanel.m_volume;
            this.SliSoundVolume.Enabled = true;
        }

        private void realPlayOpenMic()
        {
            int iRet = NETDEVSDK.NETDEV_OpenMic(m_curRealPanel.m_playhandle);
            if (NETDEVSDK.FALSE == iRet)
            {
                showFailLogInfo(m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_ip + " chl:" + (getChannelID()), "Open Mic", NETDEVSDK.NETDEV_GetLastError());
                return;
            }
            showSuccessLogInfo(m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_ip + " chl:" + (getChannelID()), "Open Mic");

            this.MicVolumeBtn.BackgroundImage = global::NetDemo.Properties.Resources._222;
            m_curRealPanel.m_micStatus = true;

            int volumeTemp = 0;
            iRet = NETDEVSDK.NETDEV_GetMicVolume(m_curRealPanel.m_playhandle, ref volumeTemp);
            if (NETDEVSDK.FALSE == iRet)
            {
                showFailLogInfo(m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_ip + " chl:" + (getChannelID()), "Get Mic volume fail", NETDEVSDK.NETDEV_GetLastError());
                return;
            }
            showSuccessLogInfo(m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_ip + " chl:" + (getChannelID()), "Get Mic volume");

            m_curRealPanel.m_micVolume = volumeTemp;
            this.SliMicVolume.Value = m_curRealPanel.m_micVolume;
            this.SliMicVolume.Enabled = true;
        }

        //stop realplay
        public bool stopRealPlay(PlayPanel selectRealPanel, bool flag)
        {
            if (IntPtr.Zero == selectRealPanel.m_playhandle)
            {
                return false;
            }

            if (NETDEVSDK.FALSE == NETDEVSDK.NETDEV_StopRealPlay(selectRealPanel.m_playhandle))
            {
                showFailLogInfo(m_deviceInfoList[selectRealPanel.m_deviceIndex].m_ip + " chl:" + (selectRealPanel.m_channelID), "stop real play", NETDEVSDK.NETDEV_GetLastError());
                return false;
            }

            if (true == flag)
            {
                RealPlayInfo objRealPlayInfo = new RealPlayInfo();
                objRealPlayInfo.m_channel = selectRealPanel.m_channelID;
                objRealPlayInfo.m_panelIndex = selectRealPanel.m_panelIndex;
                m_deviceInfoList[selectRealPanel.m_deviceIndex].removeRealPlayInfo(objRealPlayInfo);
            }

            showSuccessLogInfo(m_deviceInfoList[selectRealPanel.m_deviceIndex].m_ip + " chl:" + (selectRealPanel.m_channelID), "stop real play");

            if (true == selectRealPanel.m_twoWayAudioFlag)
            {
                TwoWayAudio_Click(null, null);
            }
            if (true == selectRealPanel.m_micStatus)
            {
                MicVolumeBtn_Click(null, null);
            }
            if (true == selectRealPanel.m_bDigitalZoomFlag)
            {
                DigitalZoom_Click(null, null);
            }

            return true;
        }

        
        private void StopRealPlay_Click(object sender, EventArgs e)
        {
            if (false == stopRealPlay(m_curRealPanel, true))
            {
                return;
            }

            m_curRealPanel.initPlayPanel();
        }

        /*mic*/
        private void SliMicVolume_Scroll(object sender, EventArgs e)
        {
            if (IntPtr.Zero == m_curRealPanel.m_playhandle)
            {
                return;
            }

            int iRet = NETDEVSDK.NETDEV_MicVolumeControl(m_curRealPanel.m_playhandle,(sender as TrackBar).Value);
            if (NETDEVSDK.FALSE == iRet)
            {
                showFailLogInfo(m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_ip + " chl:" + (getChannelID()), "Control Mic volume", NETDEVSDK.NETDEV_GetLastError());
                return;
            }
            showSuccessLogInfo(m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_ip + " chl:" + (getChannelID()), "Control Mic volume");

            m_curRealPanel.m_micVolume = (sender as TrackBar).Value;
        }

        /*Mic*/
        private void MicVolumeBtn_Click(object sender, EventArgs e)
        {
            if (IntPtr.Zero == m_curRealPanel.m_playhandle)
            {
                return;
            }

            if (true == m_curRealPanel.m_micStatus)
            {
                int iRet = NETDEVSDK.NETDEV_CloseMic(m_curRealPanel.m_playhandle);
                if (NETDEVSDK.FALSE == iRet)
                {
                    showFailLogInfo(m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_ip + " chl:" + (getChannelID()), "Close Mic fail", NETDEVSDK.NETDEV_GetLastError());
                    return;
                }
                showSuccessLogInfo(m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_ip + " chl:" + (getChannelID()), "Close Mic");

                this.MicVolumeBtn.BackgroundImage = global::NetDemo.Properties.Resources._222;
                m_curRealPanel.m_micStatus = false;
                this.SliMicVolume.Enabled = false;
            }
            else
            {
                realPlayOpenMic();
            }
        }

        private void SoundBtn_Click(object sender, EventArgs e)
        {
            if (IntPtr.Zero == m_curRealPanel.m_playhandle)
            {
                return;
            }

            if (true == m_curRealPanel.m_soundStatus)
            {
                int iRet = NETDEVSDK.NETDEV_CloseSound(m_curRealPanel.m_playhandle);
                if (NETDEVSDK.FALSE == iRet)
                {
                    showFailLogInfo(m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_ip + " chl:" + (getChannelID()), "Close volume fail", NETDEVSDK.NETDEV_GetLastError());
                    return;
                }
                showSuccessLogInfo(m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_ip + " chl:" + (getChannelID()), "Close volume");

                this.SoundBtn.BackgroundImage = global::NetDemo.Properties.Resources.ico00009;
                this.SliSoundVolume.Enabled = false;
                m_curRealPanel.m_soundStatus = false;
            }
            else
            {
                realPlayOpenSound();
            }
        }

        private void SliSoundVolume_Scroll(object sender, EventArgs e)
        {
            if (IntPtr.Zero == m_curRealPanel.m_playhandle)
            {
                return;
            }

            int iRet = NETDEVSDK.NETDEV_SoundVolumeControl(m_curRealPanel.m_playhandle, (sender as TrackBar).Value);
            if (NETDEVSDK.FALSE == iRet)
            {
                showFailLogInfo(m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_ip + " chl:" + (getChannelID()), "Control volume", NETDEVSDK.NETDEV_GetLastError());
                return;
            }
            showSuccessLogInfo(m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_ip + " chl:" + (getChannelID()), "Control volume");

            m_curRealPanel.m_volume = (sender as TrackBar).Value;
        }
        
        /************************realPlay control end ************************************/
        
        private void PannelContextMenuStrip_Opening(object sender, CancelEventArgs e)
        {
            String panelName = (sender as ContextMenuStrip).SourceControl.Name;
            int panelIndex = int.Parse(panelName.Split(' ')[1]);
            m_mourseRightSelectedPanel = arrayRealPanel[panelIndex];

            ShowDelay.Checked = m_mourseRightSelectedPanel.m_bShortDelayFlag;
            Fluent.Checked = m_mourseRightSelectedPanel.m_bFluentFlag;
            DigitalZoom.Checked = m_mourseRightSelectedPanel.m_bDigitalZoomFlag;
            ThreeDPosition.Checked = m_mourseRightSelectedPanel.m_3DPositionFlag;
            TwoWayAudio.Checked = m_mourseRightSelectedPanel.m_twoWayAudioFlag;

            realPanel_Click(m_mourseRightSelectedPanel, null);
        }

        private void closeAllChannel()
        {
            foreach (PlayPanel panel in arrayRealPanel)
            {
                if (panel.m_playStatus == true)
                {
                    m_curRealPanel = panel;
                    stopRealPlay(m_curRealPanel, true);
                    m_curRealPanel.initPlayPanel();
                }
            }
        }

        private void Delete_Click(object sender, EventArgs e)
        {
            PBCloseAllPlayBack();
            closeSelectedDeviceRealPlay(m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex], m_CurSelectTreeNodeInfo.dwDeviceIndex, true);
                        
            for (int j = 0; j < arrayRealPanel.Length; j++)
            {
                if (arrayRealPanel[j].m_deviceIndex > m_CurSelectTreeNodeInfo.dwDeviceIndex)
                {
                    arrayRealPanel[j].m_deviceIndex--;
                }
            }

            m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_eDeviceType = NETDEMO.NETDEMO_DEVICE_TYPE_E.NETDEMO_DEVICE_INVALID;

            this.DeviceTree.SelectedNode.Nodes.Clear();
            this.DeviceTree.SelectedNode.Remove();

            m_CurSelectTreeNodeInfo = new TreeNodeInfo();
        }

        
        private void Logout_Click(object sender, EventArgs e)
        {
            PBCloseAllPlayBack();

            closeSelectedDeviceRealPlay(m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex], m_CurSelectTreeNodeInfo.dwDeviceIndex, true);
        }

        private void closeSelectedDeviceRealPlay(DeviceInfo deviceInfo, int DeviceIndex, bool flag)
        {
            int channelCount = deviceInfo.m_channelNumber;
            for (int i = 0; i < channelCount; i++)
            {
                for (int j = 0; j < arrayRealPanel.Length; j++)
                {
                    if (arrayRealPanel[j].m_deviceIndex == DeviceIndex && arrayRealPanel[j].m_channelID == deviceInfo.m_channelInfoList[i].m_devVideoChlInfo.dwChannelID)
                    {
                        m_curRealPanel = arrayRealPanel[j];
                        stopRealPlay(m_curRealPanel, flag);
                        arrayRealPanel[j].initPlayPanel();
                    }
                }
            }

            /* Logout */
            if (NETDEVSDK.FALSE == NETDEVSDK.NETDEV_Logout(deviceInfo.m_lpDevHandle))
            {
                return;
            }
            deviceInfo.initDeviceInfo();
            deleteDeviceTreeNode(DeviceIndex);
            //updatedeviceTreeStatus(m_mourseRightDeviceNode.Index);
        }

        private void updatedeviceTreeStatus(int treeNodeIndex)
        {
            TreeNode treeNode = TreeViewFindNode(DeviceTree, treeNodeIndex, 0, NETDEMO.NETDEMO_FIND_TREE_NODE_TYPE_E.NETDEMO_FIND_TREE_NODE_DEVICE_INDEX);
            if (null == treeNode)
            {
                return;
            }

            if (m_deviceInfoList[treeNodeIndex].m_lpDevHandle == IntPtr.Zero)
            {
                if (treeNode.ImageIndex == NETDEMO.NETDEV_TREEVIEW_IMAGE_LOCAL_DEVICE_ON)
                {
                    treeNode.ImageIndex = NETDEMO.NETDEV_TREEVIEW_IMAGE_LOCAL_DEVICE_OFF;
                    treeNode.SelectedImageIndex = NETDEMO.NETDEV_TREEVIEW_IMAGE_LOCAL_DEVICE_OFF;
                    for (int i = 0; i < treeNode.Nodes.Count; i++)
                    {
                        treeNode.Nodes[i].ImageIndex = NETDEMO.NETDEV_TREEVIEW_IMAGE_CHL_DEVICE_OFF;
                        treeNode.Nodes[i].SelectedImageIndex = NETDEMO.NETDEV_TREEVIEW_IMAGE_CHL_DEVICE_OFF;
                    }
                }
                else if (treeNode.ImageIndex == NETDEMO.NETDEV_TREEVIEW_IMAGE_CLOUD_DEVICE_ON)
                {
                    treeNode.ImageIndex = NETDEMO.NETDEV_TREEVIEW_IMAGE_CLOUD_DEVICE_OFF;
                    treeNode.SelectedImageIndex = NETDEMO.NETDEV_TREEVIEW_IMAGE_CLOUD_DEVICE_OFF;

                    for (int i = 0; i < treeNode.Nodes.Count; i++)
                    {
                        treeNode.Nodes[i].ImageIndex = NETDEMO.NETDEV_TREEVIEW_IMAGE_CHL_DEVICE_OFF;
                        treeNode.Nodes[i].SelectedImageIndex = NETDEMO.NETDEV_TREEVIEW_IMAGE_CHL_DEVICE_OFF;
                    }
                }
            }

            else
            {
                if (treeNode.ImageIndex == NETDEMO.NETDEV_TREEVIEW_IMAGE_LOCAL_DEVICE_OFF)
                {
                    treeNode.ImageIndex = NETDEMO.NETDEV_TREEVIEW_IMAGE_LOCAL_DEVICE_ON;
                    treeNode.SelectedImageIndex = NETDEMO.NETDEV_TREEVIEW_IMAGE_LOCAL_DEVICE_ON;
                    for (int i = 0; i < treeNode.Nodes.Count; i++)
                    {
                        if (m_deviceInfoList[treeNodeIndex].m_channelInfoList[i].m_devVideoChlInfo.enStatus == (int)NETDEV_CHANNEL_STATUS_E.NETDEV_CHL_STATUS_ONLINE)
                        {
                            treeNode.Nodes[i].ImageIndex = NETDEMO.NETDEV_TREEVIEW_IMAGE_CHL_DEVICE_ON;
                            treeNode.Nodes[i].SelectedImageIndex = NETDEMO.NETDEV_TREEVIEW_IMAGE_CHL_DEVICE_ON;
                        }
                        else
                        {
                            treeNode.Nodes[i].ImageIndex = NETDEMO.NETDEV_TREEVIEW_IMAGE_CHL_DEVICE_OFF;
                            treeNode.Nodes[i].SelectedImageIndex = NETDEMO.NETDEV_TREEVIEW_IMAGE_CHL_DEVICE_OFF;
                        }
                        
                    }
                }
                else if (treeNode.ImageIndex == NETDEMO.NETDEV_TREEVIEW_IMAGE_CLOUD_DEVICE_OFF)
                {
                    treeNode.ImageIndex = NETDEMO.NETDEV_TREEVIEW_IMAGE_CLOUD_DEVICE_ON;
                    treeNode.SelectedImageIndex = NETDEMO.NETDEV_TREEVIEW_IMAGE_CLOUD_DEVICE_ON;

                    for (int i = 0; i < treeNode.Nodes.Count; i++)
                    {
                        if (m_deviceInfoList[treeNodeIndex].m_channelInfoList[i].m_devVideoChlInfo.enStatus == (int)NETDEV_CHANNEL_STATUS_E.NETDEV_CHL_STATUS_ONLINE)
                        {
                            treeNode.Nodes[i].ImageIndex = NETDEMO.NETDEV_TREEVIEW_IMAGE_CHL_DEVICE_ON;
                            treeNode.Nodes[i].SelectedImageIndex = NETDEMO.NETDEV_TREEVIEW_IMAGE_CHL_DEVICE_ON;
                        }
                        else
                        {
                            treeNode.Nodes[i].ImageIndex = NETDEMO.NETDEV_TREEVIEW_IMAGE_CHL_DEVICE_OFF;
                            treeNode.Nodes[i].SelectedImageIndex = NETDEMO.NETDEV_TREEVIEW_IMAGE_CHL_DEVICE_OFF;
                        }

                    }
                }
            }
        }

        
        private void Login_Click(object sender, EventArgs e)
        {
            /*add login fail log tip */
            switch (this.DeviceTree.SelectedNode.ImageIndex)
            {
                case  NETDEMO.NETDEV_TREEVIEW_IMAGE_LOCAL_DEVICE_OFF:
                {
                    String strIPAddr = m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_ip;
                    Int16 udwPort = m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_port;
                    String strUserName = m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_userName;
                    String strPassword = m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_password;
                    GeneralDef.NETDEMO.NETDEMO_DEVICE_TYPE_E eDeviceType = m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_eDeviceType;

                    AddLocalDevice(strIPAddr, udwPort, strUserName, strPassword, eDeviceType);
                    break;
                }
                case NETDEMO.NETDEV_TREEVIEW_IMAGE_LOCAL_DEVICE_ON:
                {
                    String strIPAddr = m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_ip;
                    Int16 udwPort = m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_port;
                    showFailLogInfo(strIPAddr + " : " + udwPort, "login", NETDEVSDK.NETDEV_GetLastError());
                    MessageBox.Show("The device already exists!");
                    return;
                }
                case NETDEMO.NETDEV_TREEVIEW_IMAGE_CLOUD_DEVICE_OFF:
                {
                    String strURL = m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_cloudUrl;
                    String strUserName = m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_cloudUserName;
                    String strPassword = m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_cloudPassword;

                    AddCloudDevice(strURL, strUserName, strPassword);
                    break;
                }
                case NETDEMO.NETDEV_TREEVIEW_IMAGE_CLOUD_DEVICE_ON:
                {
                    String strURL = m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_cloudUrl;
                    showFailLogInfo(strURL + ":" + m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_stCloudDevInfo.szDevUserName, "login", NETDEVSDK.NETDEV_GetLastError());
                    MessageBox.Show("The cloudDevice already exists!");
                    return;
                }
                default:
                    return;
            }
            
        }

        
        private void Property_Click(object sender, EventArgs e)
        {
            if (this.DeviceTree.SelectedNode.ImageIndex == NETDEMO.NETDEV_TREEVIEW_IMAGE_LOCAL_DEVICE_ON)
            {
                LocalDeviceAttribute localDeviceAttribute = new LocalDeviceAttribute(m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex]);
                localDeviceAttribute.ShowDialog();
            }
            else if (this.DeviceTree.SelectedNode.ImageIndex == NETDEMO.NETDEV_TREEVIEW_IMAGE_CLOUD_DEVICE_ON)
            {
                CloudDeviceAttribute cloudDeviceAttribute = new CloudDeviceAttribute(m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex]);
                cloudDeviceAttribute.ShowDialog();
            }
        }


        /******************** PTZ Control start by 2017-08-30 ********************/

        
        private void setPTZControlBtnStatus()
        {
            int dwChannelIndex = getChannelIndex();
            int dwSubDeviceIndex = getSubDeviceIndex();
            int dwOrgIndex = getOrgIndex();

            if (dwChannelIndex == -1)
            {
                return;
            }

            bool bPtzSupported = false;
            if (NETDEMO.NETDEMO_DEVICE_TYPE_E.NETDEMO_DEVICE_VMS == m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_eDeviceType)
            {
                if (-1 == dwOrgIndex || -1 == dwSubDeviceIndex)
                {
                    return;
                }

                if (NETDEVSDK.TRUE == m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].stVmsDevInfo.stOrgInfoList[dwOrgIndex].stVmsDevBasicInfoList[dwSubDeviceIndex].stChnInfoList[dwChannelIndex].stChnInfo.bSupportPTZ)
                {
                    bPtzSupported = true;
                }
            }
            else
            {
                if (m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_channelInfoList[dwChannelIndex].m_devVideoChlInfo.bPtzSupported == NETDEVSDK.TRUE)
                {
                    bPtzSupported = true;
                }
            }

            if (m_curRealPanel.m_playStatus == true && true == bPtzSupported)
            {
                this.ptzLUBtn.Enabled = true;
                this.PTZUBtn.Enabled = true;
                this.PTZRUBtn.Enabled = true;
                this.PTZLBtn.Enabled = true;
                this.PTZStopBtn.Enabled = true;
                this.PTZRBtn.Enabled = true;
                this.PTZLDBtn.Enabled = true;
                this.PTZDBtn.Enabled = true;
                this.PTZRDBtn.Enabled = true;

                this.zoomTeleBtn.Enabled = true;
                this.zoomWideBtn.Enabled = true;
                this.focusFarBtn.Enabled = true;
                this.focusNearBtn.Enabled = true;

                this.MoreBtn.Enabled = true;
            }
            else
            {
                this.ptzLUBtn.Enabled = false;
                this.PTZUBtn.Enabled = false;
                this.PTZRUBtn.Enabled = false;
                this.PTZLBtn.Enabled = false;
                this.PTZStopBtn.Enabled = false;
                this.PTZRBtn.Enabled = false;
                this.PTZLDBtn.Enabled = false;
                this.PTZDBtn.Enabled = false;
                this.PTZRDBtn.Enabled = false;

                this.zoomTeleBtn.Enabled = false;
                this.zoomWideBtn.Enabled = false;
                this.focusFarBtn.Enabled = false;
                this.focusNearBtn.Enabled = false;

                this.MoreBtn.Enabled = false;
            }
        }

        
        private void PTZSpeedTrackBar_Scroll(object sender, EventArgs e)
        {
            m_oPtzControl.setPTZSpeed((sender as TrackBar).Value);
        }

        
        private void PTZStopBtn_Click(object sender, EventArgs e)
        {
            m_oPtzControl.control(m_curRealPanel.m_playhandle, (int)NETDEV_PTZ_E.NETDEV_PTZ_ALLSTOP);
        }

        
        private void ptzLUBtn_MouseDown(object sender, MouseEventArgs e)
        {
            m_oPtzControl.control(m_curRealPanel.m_playhandle, (int)NETDEV_PTZ_E.NETDEV_PTZ_LEFTUP);
        }

        private void ptzLUBtn_MouseUp(object sender, MouseEventArgs e)
        {
            m_oPtzControl.control(m_curRealPanel.m_playhandle, (int)NETDEV_PTZ_E.NETDEV_PTZ_ALLSTOP);
        }

        
        private void PTZUBtn_MouseDown(object sender, MouseEventArgs e)
        {
            m_oPtzControl.control(m_curRealPanel.m_playhandle, (int)NETDEV_PTZ_E.NETDEV_PTZ_TILTUP);
        }

        private void PTZUBtn_MouseUp(object sender, MouseEventArgs e)
        {
            m_oPtzControl.control(m_curRealPanel.m_playhandle, (int)NETDEV_PTZ_E.NETDEV_PTZ_ALLSTOP);
        }

        
        private void PTZRUBtn_MouseDown(object sender, MouseEventArgs e)
        {
            m_oPtzControl.control(m_curRealPanel.m_playhandle, (int)NETDEV_PTZ_E.NETDEV_PTZ_RIGHTUP);
        }

        private void PTZRUBtn_MouseUp(object sender, MouseEventArgs e)
        {
            m_oPtzControl.control(m_curRealPanel.m_playhandle, (int)NETDEV_PTZ_E.NETDEV_PTZ_ALLSTOP);
        }

        
        private void PTZLBtn_MouseDown(object sender, MouseEventArgs e)
        {
            m_oPtzControl.control(m_curRealPanel.m_playhandle, (int)NETDEV_PTZ_E.NETDEV_PTZ_PANLEFT);
        }

        private void PTZLBtn_MouseUp(object sender, MouseEventArgs e)
        {
            m_oPtzControl.control(m_curRealPanel.m_playhandle, (int)NETDEV_PTZ_E.NETDEV_PTZ_ALLSTOP);
        }

        
        private void PTZRBtn_MouseDown(object sender, MouseEventArgs e)
        {
            m_oPtzControl.control(m_curRealPanel.m_playhandle, (int)NETDEV_PTZ_E.NETDEV_PTZ_PANRIGHT);
        }

        private void PTZRBtn_MouseUp(object sender, MouseEventArgs e)
        {
            m_oPtzControl.control(m_curRealPanel.m_playhandle, (int)NETDEV_PTZ_E.NETDEV_PTZ_ALLSTOP);
        }

        
        private void PTZLDBtn_MouseDown(object sender, MouseEventArgs e)
        {
            m_oPtzControl.control(m_curRealPanel.m_playhandle, (int)NETDEV_PTZ_E.NETDEV_PTZ_LEFTDOWN);
        }

        private void PTZLDBtn_MouseUp(object sender, MouseEventArgs e)
        {
            m_oPtzControl.control(m_curRealPanel.m_playhandle, (int)NETDEV_PTZ_E.NETDEV_PTZ_ALLSTOP);
        }

        
        private void PTZDBtn_MouseDown(object sender, MouseEventArgs e)
        {
            m_oPtzControl.control(m_curRealPanel.m_playhandle, (int)NETDEV_PTZ_E.NETDEV_PTZ_TILTDOWN);
        }

        private void PTZDBtn_MouseUp(object sender, MouseEventArgs e)
        {
            m_oPtzControl.control(m_curRealPanel.m_playhandle, (int)NETDEV_PTZ_E.NETDEV_PTZ_ALLSTOP);
        }

        
        private void PTZRDBtn_MouseDown(object sender, MouseEventArgs e)
        {
            m_oPtzControl.control(m_curRealPanel.m_playhandle, (int)NETDEV_PTZ_E.NETDEV_PTZ_RIGHTDOWN);
        }

        private void PTZRDBtn_MouseUp(object sender, MouseEventArgs e)
        {
            m_oPtzControl.control(m_curRealPanel.m_playhandle, (int)NETDEV_PTZ_E.NETDEV_PTZ_ALLSTOP);
        }


        private void zoomWideBtn_MouseDown(object sender, MouseEventArgs e)
        {
            m_oPtzControl.control(m_curRealPanel.m_playhandle, (int)NETDEV_PTZ_E.NETDEV_PTZ_ZOOMWIDE);
        }

        private void zoomWideBtn_MouseUp(object sender, MouseEventArgs e)
        {
            m_oPtzControl.control(m_curRealPanel.m_playhandle, (int)NETDEV_PTZ_E.NETDEV_PTZ_ZOOMWIDE_STOP);
        }

        private void zoomTeleBtn_MouseDown(object sender, MouseEventArgs e)
        {
            m_oPtzControl.control(m_curRealPanel.m_playhandle, (int)NETDEV_PTZ_E.NETDEV_PTZ_ZOOMTELE);
        }

        private void zoomTeleBtn_MouseUp(object sender, MouseEventArgs e)
        {
            m_oPtzControl.control(m_curRealPanel.m_playhandle, (int)NETDEV_PTZ_E.NETDEV_PTZ_ZOOMTELE_STOP);
        }

        private void focusNearBtn_MouseDown(object sender, MouseEventArgs e)
        {
            m_oPtzControl.control(m_curRealPanel.m_playhandle, (int)NETDEV_PTZ_E.NETDEV_PTZ_FOCUSNEAR);
        }

        private void focusNearBtn_MouseUp(object sender, MouseEventArgs e)
        {
            m_oPtzControl.control(m_curRealPanel.m_playhandle, (int)NETDEV_PTZ_E.NETDEV_PTZ_FOCUSNEAR_STOP);
        }

        private void focusFarBtn_MouseDown(object sender, MouseEventArgs e)
        {
            m_oPtzControl.control(m_curRealPanel.m_playhandle, (int)NETDEV_PTZ_E.NETDEV_PTZ_FOCUSFAR);
        }

        private void focusFarBtn_MouseUp(object sender, MouseEventArgs e)
        {
            m_oPtzControl.control(m_curRealPanel.m_playhandle, (int)NETDEV_PTZ_E.NETDEV_PTZ_FOCUSFAR_STOP);
        }

        
        public void presetGetBtn_Click(object sender, EventArgs e)
        {
            if (getChannelID() == -1)
            {
                return;
            }

            int  dwChannelID = getChannelID();
            IntPtr lpHandle = m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_lpDevHandle;

            
            if (IntPtr.Zero == lpHandle)
            {
                MessageBox.Show("Device Handle is 0 ","warning");
                return;
            }

            this.presetIDCobBox.Items.Clear();

            NETDEV_PTZ_ALLPRESETS_S stPtzPresets = new NETDEV_PTZ_ALLPRESETS_S();

            Int32 dwBytesReturned = 0;
            int iRet = NETDEVSDK.NETDEV_GetPTZPresetList(lpHandle, dwChannelID, ref stPtzPresets);
            if (NETDEVSDK.TRUE != iRet)
            {
                if (NETDEVSDK.NETDEV_E_NO_RESULT == NETDEVSDK.NETDEV_GetLastError())
                {
                    showFailLogInfo(m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_ip + " chl:" + (getChannelID()), "Preset list is emtpy.", NETDEVSDK.NETDEV_GetLastError());
                }
                else
                {
                    showFailLogInfo(m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_ip + " chl:" + (getChannelID()), "get Preset", NETDEVSDK.NETDEV_GetLastError());
                }
                return;
            }
            else
            {
                Int32 i = 0;
                for (; i < stPtzPresets.dwSize; i++)
                {
                    this.presetIDCobBox.Items.Add(Convert.ToString(stPtzPresets.astPreset[i].dwPresetID));
                }

                if (i > 0)
                {
                    presetIDCobBox.SelectedIndex = 0;
                    presetNameText.Text = GetDefaultString(stPtzPresets.astPreset[0].szPresetName);
                }
                else
                {
                    presetNameText.Text = "";
                }
                showSuccessLogInfo(m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_ip + " chl:" + (getChannelID()), "get Preset");                
            }
        }

        
        private void presetGoToBtn_Click(object sender, EventArgs e)
        {
            if (getChannelID() == -1)
            {
                return;
            }

            int  dwChannelID = getChannelID();
            IntPtr lpHandle = m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_lpDevHandle;

            
            if (IntPtr.Zero == lpHandle)
            {
                MessageBox.Show("Device Handle is 0 ","warning");
                return;
            }

            int presetID = Convert.ToInt32(this.presetIDCobBox.SelectedItem);

            string PresetNameTemp = "";

            byte[] byPresetName;
            GetUTF8Buffer(PresetNameTemp, NETDEVSDK.NETDEV_LEN_32, out byPresetName);

            int iRet = NETDEVSDK.NETDEV_PTZPreset_Other(lpHandle, dwChannelID, (int)NETDEV_PTZ_PRESETCMD_E.NETDEV_PTZ_GOTO_PRESET, byPresetName, presetID);
            if(NETDEVSDK.TRUE != iRet)
            {
                showFailLogInfo(m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_ip + " chl:" + (getChannelID()), "Go to preset fail", NETDEVSDK.NETDEV_GetLastError());
                return;
            }
            showSuccessLogInfo(m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_ip + " chl:" + (getChannelID()), "Go to preset");

        }

        private void presetIDCobBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (getChannelID() == -1)
            {
                return;
            }

            int dwChannelID = getChannelID();
            IntPtr lpHandle = m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_lpDevHandle;

            if (IntPtr.Zero == lpHandle)
            {
                MessageBox.Show("Device Handle is 0 ","warning");
                return;
            }

            NETDEV_PTZ_ALLPRESETS_S stPtzPresets = new NETDEV_PTZ_ALLPRESETS_S();
            Int32 dwBytesReturned = 0;
            int iRet = NETDEVSDK.NETDEV_GetPTZPresetList(lpHandle, dwChannelID, ref stPtzPresets);
            if (NETDEVSDK.TRUE != iRet)
            {
                if (NETDEVSDK.NETDEV_E_NO_RESULT == NETDEVSDK.NETDEV_GetLastError())
                {
                    showFailLogInfo(m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_ip + " chl:" + (getChannelID()), "Preset list is emtpy.", NETDEVSDK.NETDEV_GetLastError());
                }
                else
                {
                    showFailLogInfo(m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_ip + " chl:" + (getChannelID()), "get Preset", NETDEVSDK.NETDEV_GetLastError());
                }
                return;
            }
            else
            {
                for (Int32 i = 0; i < stPtzPresets.dwSize; i++)
                {
                    if (Convert.ToInt32(this.presetIDCobBox.SelectedItem) == stPtzPresets.astPreset[i].dwPresetID)
                    {
                        presetNameText.Text = GetDefaultString(stPtzPresets.astPreset[i].szPresetName);
                        return;
                    }
                }
                    
                presetNameText.Text = "";
                showSuccessLogInfo(m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_ip + " chl:" + (getChannelID()), "get Preset");
            }
        }

        private void presetSetBtn_Click(object sender, EventArgs e)
        {
            Preset oPreset = new Preset(this);
            oPreset.ShowDialog();
        }

        private void presetDeleteBtn_Click(object sender, EventArgs e)
        {
            if (this.getChannelID() == -1)
            {
                return;
            }

            int dwChannelID = this.getChannelID();
            IntPtr lpHandle = this.getDeviceInfoList()[this.m_CurSelectTreeNodeInfo.dwDeviceIndex].m_lpDevHandle;

            if (IntPtr.Zero == lpHandle)
            {
                MessageBox.Show("Device Handle is 0 ","warning");
                return;
            }

            String strPresetName = "";
            Int32 lPresetID = Convert.ToInt32(this.presetIDCobBox.SelectedItem);

            byte[] byPresetName;
            GetUTF8Buffer(strPresetName, NETDEVSDK.NETDEV_LEN_32, out byPresetName);

            int iRet = NETDEVSDK.NETDEV_PTZPreset_Other(lpHandle, dwChannelID, (int)NETDEV_PTZ_PRESETCMD_E.NETDEV_PTZ_CLE_PRESET, byPresetName, lPresetID);
            if (NETDEVSDK.TRUE != iRet)
            {
                showFailLogInfo(m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_ip + " chl:" + (getChannelID()), "Delete preset fail", NETDEVSDK.NETDEV_GetLastError());
            }
            else
            {
                this.presetGetBtn_Click(null, null);
                showSuccessLogInfo(m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_ip + " chl:" + (getChannelID()), "Delete preset");
            }
        }

        /******************** PTZ Control end ********************/

        public PlayPanel getCurRealPanel()
        {
            return m_curRealPanel;
        }

        public List<DeviceInfo> getDeviceInfoList()
        {
            return m_deviceInfoList;
        }

        public CycleMonitorInfo getCycleMonitorInfo()
        {
            return this.m_cycleMonitorInfo;
        }

        private void CapturePicture_Click(object sender, EventArgs e)
        {
            if (m_curRealPanel.m_playhandle == IntPtr.Zero)
            {
                if (m_CurSelectTreeNodeInfo.dwDeviceIndex > m_deviceInfoList.Count() || m_CurSelectTreeNodeInfo.dwDeviceIndex < 0)
                {
                    return;
                }

                String strNoPreviewTemp = string.Copy(LocalSetting.m_strPicSavePath);
                DateTime oNoPreviewDate = DateTime.Now;
                String strNoPreviewCurTime = oNoPreviewDate.ToString("yyMMddHHmmss", DateTimeFormatInfo.InvariantInfo);
                LocalSetting.m_strPicSavePath += "\\";
                LocalSetting.m_strPicSavePath += m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_ip;
                LocalSetting.m_strPicSavePath += "_";
                LocalSetting.m_strPicSavePath += (getChannelID());
                LocalSetting.m_strPicSavePath += "_";
                LocalSetting.m_strPicSavePath += strNoPreviewCurTime;

                byte[] picNoPreviewSavePath;
                GetUTF8Buffer(LocalSetting.m_strPicSavePath, NETDEVSDK.NETDEV_LEN_260, out picNoPreviewSavePath);

                int iiRet = NETDEVSDK.NETDEV_CaptureNoPreview(m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_lpDevHandle, getChannelID(), (int)NETDEV_LIVE_STREAM_INDEX_E.NETDEV_LIVE_STREAM_INDEX_MAIN, LocalSetting.m_strPicSavePath, (int)NETDEV_PICTURE_FORMAT_E.NETDEV_PICTURE_BMP);
                if (NETDEVSDK.FALSE == iiRet)
                {
                    showFailLogInfo(m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_ip + " chl:" + (getChannelID()), "CaptureNoPreview", NETDEVSDK.NETDEV_GetLastError());
                    LocalSetting.m_strPicSavePath = strNoPreviewTemp;
                    return;
                }
                showSuccessLogInfo(m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_ip + " chl:" + (getChannelID()), "CaptureNoPreview");
                LocalSetting.m_strPicSavePath = strNoPreviewTemp;
                return;
            }

            String strTemp = string.Copy(LocalSetting.m_strPicSavePath);
            DateTime oDate = DateTime.Now;
            String strCurTime = oDate.ToString("yyMMddHHmmss", DateTimeFormatInfo.InvariantInfo);
            LocalSetting.m_strPicSavePath += "\\";
            LocalSetting.m_strPicSavePath += m_deviceInfoList[m_curRealPanel.m_deviceIndex].m_ip;
            LocalSetting.m_strPicSavePath += "_";
            LocalSetting.m_strPicSavePath += (m_curRealPanel.m_channelID);
            LocalSetting.m_strPicSavePath += "_";
            LocalSetting.m_strPicSavePath += strCurTime;

            byte[] picSavePath;
            GetUTF8Buffer(LocalSetting.m_strPicSavePath, NETDEVSDK.NETDEV_LEN_260, out picSavePath);

            int iRet = NETDEVSDK.NETDEV_CapturePicture(m_curRealPanel.m_playhandle, picSavePath, (int)NETDEV_PICTURE_FORMAT_E.NETDEV_PICTURE_BMP);
            if (NETDEVSDK.FALSE == iRet)
            {
                showFailLogInfo(m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_ip + " chl:" + (getChannelID()), "CapturePicture", NETDEVSDK.NETDEV_GetLastError());
                //return;
            }

            showSuccessLogInfo(m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_ip + " chl:" + (getChannelID()), "CapturePicture");

            LocalSetting.m_strPicSavePath = strTemp;
        }

        
        public void startCycleMonitorThread()
        {
           // m_cycleMonitorThread = new Thread(new ThreadStart(cycleMonitorRun));
         (new Thread(new ThreadStart(cycleMonitorRun))).Start();
            //m_cycleMonitorThread.Start();
        }

        public void startKeepAliveDeviceThread()
        {
            m_keepAliveDeviceThread = new Thread(new ThreadStart(keepAliveRun));
            m_keepAliveDeviceThread.Start();
        }

        //add local device
        public void AddLocalDevice(DeviceInfo deviceInfo)
        {
            lock (m_notLoggeddeviceInfoListlocker)
            {
                m_notLoggeddeviceInfoList.Add(deviceInfo);
            }
        }

        private void keepAliveRun()
        {
            while (true)
            {
                //update local device list
                //for (int i = 0; i < m_deviceInfoList.Count; i++)
                //{
                //    if (NETDEMO.NETDEMO_DEVICE_TYPE_E.NETDEMO_DEVICE_INVALID == m_deviceInfoList[i].m_eDeviceType)
                //    {
                //        continue;
                //    }

                //    if (IntPtr.Zero == m_deviceInfoList[i].m_lpCloudDevHandle && IntPtr.Zero == m_deviceInfoList[i].m_lpDevHandle)
                //    {
                //        AddLocalDevice(m_deviceInfoList[i]);
                //    }
                //}

                //update cloud device list
                //for (int i = 0; i < m_deviceInfoList.Count; i++)
                //{
                //    if (NETDEMO.NETDEMO_DEVICE_TYPE_E.NETDEMO_DEVICE_INVALID == m_deviceInfoList[i].m_eDeviceType)
                //    {
                //        continue;
                //    }

                //    if (IntPtr.Zero != m_deviceInfoList[i].m_lpCloudDevHandle)
                //    {
                //        String strURL = m_deviceInfoList[i].m_cloudUrl;
                //        String strUserName = m_deviceInfoList[i].m_cloudUserName;
                //        String strPassword = m_deviceInfoList[i].m_cloudPassword;

                //        AddCloudDevice(strURL, strUserName, strPassword);
                //        break;
                //    }
                //}

                //login device
                List<DeviceInfo> oList = new List<DeviceInfo>();
                lock (m_notLoggeddeviceInfoListlocker)
                {
                    for (int i = 0; i < m_notLoggeddeviceInfoList.Count; ++i)
                    {
                        oList.Add(m_notLoggeddeviceInfoList[i]);
                    }
                    m_notLoggeddeviceInfoList.Clear();
                }
                
                for (int i = 0; i < oList.Count;)
                {
                    if (IntPtr.Zero != oList[i].m_lpCloudDevHandle)
                    {
                        if (NETDEVSDK.TRUE == oList[i].m_stCloudDevInfo.bKeepLiveStatus)
                        {
                            loginCloudDevice(oList[i]);
                        }
                    }
                    else
                    {
                        LoginLocalDevice(oList[i]);
                    }

                    oList.Remove(oList[i]);
                }
                
                Thread.Sleep(2 * 1000);
            }
        }
                
        private void cycleMonitorRun()
        {
            int panelIndex = 0;
            int monitorIndex = 0;
            while(true)
            {
                while (monitorIndex < m_cycleMonitorInfo.monitorCount)
                {
                    NETDEV_PREVIEWINFO_S stPreviewInfo = new NETDEV_PREVIEWINFO_S();
                    stPreviewInfo.dwChannelID = m_cycleMonitorInfo.channelInfoList[monitorIndex].channelID;
                    stPreviewInfo.dwLinkMode = (int)NETDEV_PROTOCAL_E.NETDEV_TRANSPROTOCAL_RTPTCP;
                    stPreviewInfo.dwStreamType = (int)NETDEV_LIVE_STREAM_INDEX_E.NETDEV_LIVE_STREAM_INDEX_AUX;
                    if (this.m_cycleMonitorInfo.monitorType == NETDEMO.NETDEMO_MONITOR_TYPE_E.NETDEMO_MONITOR_SINGLE_SCREEN)
                    {
                        stPreviewInfo.hPlayWnd = arrayRealPanel[m_cycleMonitorInfo.panelNo].Handle;

                        if (arrayRealPanel[m_cycleMonitorInfo.panelNo].m_playStatus == true)
                        {
                            stopRealPlay(arrayRealPanel[m_cycleMonitorInfo.panelNo], true);
                            arrayRealPanel[m_cycleMonitorInfo.panelNo].initPlayPanel();
                        }

                        IntPtr Handle = NETDEVSDK.NETDEV_RealPlay(m_cycleMonitorInfo.channelInfoList[monitorIndex].devhandle, ref stPreviewInfo, IntPtr.Zero, IntPtr.Zero);
                        if (Handle == IntPtr.Zero)
                        {
                            monitorIndex++;
                            continue;
                        }

                        arrayRealPanel[m_cycleMonitorInfo.panelNo].m_playStatus = true;
                        arrayRealPanel[m_cycleMonitorInfo.panelNo].m_playhandle = Handle;
                        arrayRealPanel[m_cycleMonitorInfo.panelNo].m_deviceIndex = m_cycleMonitorInfo.channelInfoList[monitorIndex].deviceIndex;
                        arrayRealPanel[m_cycleMonitorInfo.panelNo].m_channelID = m_cycleMonitorInfo.channelInfoList[monitorIndex].channelID;

                        monitorIndex++;
                        if (monitorIndex == m_cycleMonitorInfo.monitorCount)
                        {
                            monitorIndex = 0;
                        }
                        break;
                    }
                    else
                    {
                        stPreviewInfo.hPlayWnd = arrayRealPanel[panelIndex].Handle;

                        if (arrayRealPanel[panelIndex].m_playStatus == true)
                        {
                            stopRealPlay(arrayRealPanel[panelIndex], true);
                            arrayRealPanel[panelIndex].initPlayPanel();
                        }

                        IntPtr Handle = NETDEVSDK.NETDEV_RealPlay(m_cycleMonitorInfo.channelInfoList[monitorIndex].devhandle, ref stPreviewInfo, IntPtr.Zero, IntPtr.Zero);
                        if (Handle == IntPtr.Zero)
                        {
                            monitorIndex++;
                            if (monitorIndex == m_cycleMonitorInfo.monitorCount)
                            {
                                monitorIndex = 0;
                                if (m_cycleMonitorInfo.monitorCount <= arrayRealPanel.Length)
                                {
                                    panelIndex = monitorIndex;
                                }
                                break;
                            }
                            continue;
                        }

                        arrayRealPanel[panelIndex].m_playStatus = true;
                        arrayRealPanel[panelIndex].m_playhandle = Handle;
                        arrayRealPanel[panelIndex].m_deviceIndex = m_cycleMonitorInfo.channelInfoList[monitorIndex].deviceIndex;
                        arrayRealPanel[panelIndex].m_channelID = m_cycleMonitorInfo.channelInfoList[monitorIndex].channelID;

                        panelIndex++;
                        monitorIndex++;

                        if(panelIndex == arrayRealPanel.Length)
                        {
                            panelIndex = 0;
                            if (monitorIndex == m_cycleMonitorInfo.monitorCount)
                            {
                                monitorIndex = 0;
                            }
                            break;
                        }

                        if (monitorIndex == m_cycleMonitorInfo.monitorCount)
                        {
                            monitorIndex = 0;
                            if (m_cycleMonitorInfo.monitorCount <= arrayRealPanel.Length)
                            {
                                panelIndex = monitorIndex;
                                break;
                            }
                        }
                    }
                }

                Thread.Sleep(m_cycleMonitorInfo.intervalTime * 1000);
            }
        }

        public void stopKeepAliveDeviceThread()
        {
            try
            {
                if (m_keepAliveDeviceThread != null)
                {
                    this.m_keepAliveDeviceThread.Abort();
                    this.m_keepAliveDeviceThread = null;
                }
            }
            catch (ThreadAbortException)
            {
                MessageBox.Show("Stop keep alive thread abort");
            }
        }

        public void stopCycleMonitorThread()
        {
            try
            {
                if (m_cycleMonitorThread != null)
                {
                    this.m_cycleMonitorThread.Abort();
                    this.m_cycleMonitorThread = null;
                    this.closeAllChannel();
                }
            }
            catch (ThreadAbortException)
            {
                MessageBox.Show("Cycle monitor thread abort");
            }
        }

        private void NetDemo_FormClosing(object sender, FormClosingEventArgs e)
        {
            DialogResult result = MessageBox.Show("傻逼真要关闭吗?", "Info", MessageBoxButtons.OKCancel, MessageBoxIcon.Information);
            if (result == DialogResult.OK)
            {
                e.Cancel = false;  //OK
                this.stopCycleMonitorThread();
                this.stopKeepAliveDeviceThread();
                NETDEVSDK.NETDEV_Cleanup();
            }
            else
            {
                e.Cancel = true;
            }
        }
        
        private void PBQueryBtn_Click(object sender, EventArgs e)
        {
            int dwChannelIndex = getChannelIndex();
            int dwSubDeviceIndex = getSubDeviceIndex();
            int dwOrgIndex = getOrgIndex();

            if (dwChannelIndex == -1)
            {
                return;
            }

            PBVideoTimeListView.Items.Clear();
            m_playBackInfo.m_findPlayBackDataList.Clear();

            NETDEV_FILECOND_S stFileCond = new NETDEV_FILECOND_S();

            String beginDateTimeStr = getInputStartDataTime();
            String endDateTimeStr = getInputEndDataTime();

            stFileCond.tBeginTime = this.getLongTime(beginDateTimeStr);
            stFileCond.tEndTime = this.getLongTime(endDateTimeStr);

            if (stFileCond.tBeginTime >= stFileCond.tEndTime)
            {
                return;
            }

            switch (PBEventType.SelectedIndex)
            {
                case 0:
                    {
                        stFileCond.dwFileType = (int)NETDEV_PLAN_STORE_TYPE_E.NETDEV_TYPE_STORE_TYPE_ALL;
                    }
                    break;
                case 1:
                    {
                        stFileCond.dwFileType = (int)NETDEV_PLAN_STORE_TYPE_E.NETDEV_EVENT_STORE_TYPE_MOTIONDETECTION;
                    }
                    break;
                case 2:
                    {
                        stFileCond.dwFileType = (int)NETDEV_PLAN_STORE_TYPE_E.NETDEV_EVENT_STORE_TYPE_DIGITALINPUT;
                    }
                    break;
                case 3:
                    {
                        stFileCond.dwFileType = (int)NETDEV_PLAN_STORE_TYPE_E.NETDEV_EVENT_STORE_TYPE_VIDEOLOSS;
                    }
                    break;
                default:
                    break;
            }

            stFileCond.dwChannelID = getChannelID();
            m_playBackInfo.m_devHandle = m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_lpDevHandle;

            IntPtr dwFileHandle = NETDEVSDK.NETDEV_FindFile(m_playBackInfo.m_devHandle, ref stFileCond);

            if (dwFileHandle == IntPtr.Zero)
            {
                showFailLogInfo(m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_ip + " chl:" + (getChannelID()), "find playBack record File fail", NETDEVSDK.NETDEV_GetLastError());
                return;
            }
            showSuccessLogInfo(m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_ip + " chl:" + (getChannelID()), "find playBack record File");
                
            m_playBackInfo.m_curSelectedChannelID = stFileCond.dwChannelID;
            m_playBackInfo.m_curSelectedDeviceIndex = m_CurSelectTreeNodeInfo.dwDeviceIndex;
            NETDEV_FINDDATA_S findData = new NETDEV_FINDDATA_S();
            while (NETDEVSDK.TRUE == NETDEVSDK.NETDEV_FindNextFile(dwFileHandle, ref findData))
            {
                m_playBackInfo.m_findPlayBackDataList.Add(findData);
            }

            if(NETDEVSDK.FALSE == NETDEVSDK.NETDEV_FindClose(dwFileHandle))
            {
                showFailLogInfo(m_deviceInfoList[m_playBackInfo.m_curSelectedDeviceIndex].m_ip + " chl:" + m_playBackInfo.m_curSelectedChannelID, "close find playBack record File fail", NETDEVSDK.NETDEV_GetLastError());
                return;
            }
            showSuccessLogInfo(m_deviceInfoList[m_playBackInfo.m_curSelectedDeviceIndex].m_ip + " chl:" + m_playBackInfo.m_curSelectedChannelID, "close find playBack record File");

            showPlayBackData();
            startTimer();
        }

        
        private string getInputStartDataTime()
        {
            String beginDateTimeStr = this.PBBeginDate.Value.Year.ToString();
            beginDateTimeStr += ("-" + this.PBBeginDate.Value.Month.ToString());
            beginDateTimeStr += ("-" + this.PBBeginDate.Value.Day.ToString());

            beginDateTimeStr += (" " + this.PBBeginTime.Value.Hour.ToString());
            beginDateTimeStr += (":" + this.PBBeginTime.Value.Minute.ToString());
            beginDateTimeStr += (":" + this.PBBeginTime.Value.Second.ToString());

            return beginDateTimeStr;
        }
        
        private string getInputEndDataTime()
        {
            String endDateTimeStr = this.PBEndDate.Value.Year.ToString();
            endDateTimeStr += ("-" + this.PBEndDate.Value.Month.ToString());
            endDateTimeStr += ("-" + this.PBEndDate.Value.Day.ToString());

            endDateTimeStr += (" " + this.PBEndTime.Value.Hour.ToString());
            endDateTimeStr += (":" + this.PBEndTime.Value.Minute.ToString());
            endDateTimeStr += (":" + this.PBEndTime.Value.Second.ToString());
            return endDateTimeStr;
        }

        /* long -> string */
        private string convertRemainTime(long remainTime)
        {
            String timeTemp = "";
            timeTemp += Convert.ToString(remainTime / 3600);
            timeTemp += ":";
            timeTemp += Convert.ToString((remainTime % 3600) / 60);
            timeTemp += ":";
            timeTemp += Convert.ToString((remainTime % 3600) % 60);
            return timeTemp;
        }

        
        private void showPlayBackData()
        {
            foreach (NETDEV_FINDDATA_S findData in m_playBackInfo.m_findPlayBackDataList)
            {
                DateTime startDateTime = TimeZone.CurrentTimeZone.ToLocalTime(new System.DateTime(1970, 1, 1)); // 
                DateTime beginDateTime = startDateTime.AddSeconds(findData.tBeginTime);
                DateTime endDateTime = startDateTime.AddSeconds(findData.tEndTime);

                ListViewItem listViewItem = new ListViewItem(beginDateTime.ToString("yyyy/MM/dd HH:mm:ss"));
                listViewItem.SubItems.Add(endDateTime.ToString("yyyy/MM/dd HH:mm:ss"));
                this.PBVideoTimeListView.Items.Add(listViewItem);
                listViewItem.EnsureVisible();
            }

            if (m_playBackInfo.m_findPlayBackDataList.Count > 0)
            {
                setPlayBackControlEnable(true);
            }
            else
            {
                setPlayBackControlEnable(false);
            }
        }

        
        private void setPlayBackControlEnable(bool flag)
        {
            if (true == flag)
            {
                PBStartBtn.Enabled = true;
                PBPauseBtn.Enabled = true;
                PBStopBtn.Enabled = true;
                PBFastBackwardBtn.Enabled = true;
                PBFastForwardBtn.Enabled = true;
                PBCaptureBtn.Enabled = true;
                PBRestartBtn.Enabled = true;
                PBFrameBtn.Enabled = true;
                PBVolBtn.Enabled = true;
                PBVolTrackBar.Enabled = true;
                PBVideoTrackBar.Enabled = true;
            }
            else
            {
                PBStartBtn.Enabled = false;
                PBPauseBtn.Enabled = false;
                PBStopBtn.Enabled = false;
                PBFastBackwardBtn.Enabled = false;
                PBFastForwardBtn.Enabled = false;
                PBCaptureBtn.Enabled = false;
                PBRestartBtn.Enabled = false;
                PBFrameBtn.Enabled = false;
                PBVolBtn.Enabled = false;
                PBVolTrackBar.Enabled = false;
                PBVideoTrackBar.Enabled = false;
            }
        }

        
        private void PBVideoTimeListView_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (PBVideoTimeListView.SelectedItems.Count == 0)
            {
                return;
            }
            int selectedIndex = this.PBVideoTimeListView.SelectedIndices[0];
            String startDateTime = PBVideoTimeListView.Items[selectedIndex].SubItems[0].Text;
            String endDateTime = PBVideoTimeListView.Items[selectedIndex].SubItems[1].Text;
            this.PBBeginDate.Text = startDateTime.Split(' ')[0];
            this.PBBeginTime.Text = startDateTime.Split(' ')[1];

            this.PBEndDate.Text = endDateTime.Split(' ')[0];
            this.PBEndTime.Text = endDateTime.Split(' ')[1];
        }

        
        private void PBStartBtn_Click(object sender, EventArgs e)
        {
            if (PBVideoTimeListView.SelectedItems.Count == 0)
            {
                return;
            }

            if(m_curPlayBackPanel.m_playStatus == true && m_curPlayBackPanel.m_pauseStatus == true)
            {
                pausePlayBack(false);
                return;
            }
            m_curPlayBackPanel = arrayPlayBackPanel[m_playBackInfo.m_nextPlayBackPanelIndex];
            m_curPlayBackPanel.m_panelIndex = m_playBackInfo.m_nextPlayBackPanelIndex;
            m_playBackInfo.m_nextPlayBackPanelIndex++;
            this.setPlayBackPanelBorderColor();

            if (m_curPlayBackPanel.m_playStatus == true)
            {
                stopPlayBack();
            }

            if (m_playBackInfo.m_nextPlayBackPanelIndex == arrayPlayBackPanel.Length)
            {
                m_playBackInfo.m_nextPlayBackPanelIndex = 0;
            }

            NETDEV_PLAYBACKCOND_S playBackByTimeInfo = new NETDEV_PLAYBACKCOND_S();

            String beginDateTimeStr = getInputStartDataTime();
            String endDateTimeStr = getInputEndDataTime();

            playBackByTimeInfo.tBeginTime = this.getLongTime(beginDateTimeStr);
            playBackByTimeInfo.tEndTime = this.getLongTime(endDateTimeStr);

            playBackByTimeInfo.dwChannelID = m_playBackInfo.m_curSelectedChannelID;
            playBackByTimeInfo.dwLinkMode = (int)NETDEV_PROTOCAL_E.NETDEV_TRANSPROTOCAL_RTPTCP;
            playBackByTimeInfo.dwPlaySpeed = (int)NETDEV_VOD_PLAY_STATUS_E.NETDEV_PLAY_STATUS_1_FORWARD;
            playBackByTimeInfo.hPlayWnd = m_curPlayBackPanel.Handle;
            m_curPlayBackPanel.m_maxVideoSliderValue = (int)(playBackByTimeInfo.tEndTime - playBackByTimeInfo.tBeginTime);
            m_curPlayBackPanel.m_startTime = playBackByTimeInfo.tBeginTime;
            m_curPlayBackPanel.m_endTime = playBackByTimeInfo.tEndTime;

            PBVideoTrackBar.SetRange(0, m_curPlayBackPanel.m_maxVideoSliderValue);

            IntPtr playBackHandle = NETDEVSDK.NETDEV_PlayBackByTime(m_playBackInfo.m_devHandle, ref playBackByTimeInfo);
            if(playBackHandle == IntPtr.Zero)
            {
                showFailLogInfo(m_deviceInfoList[m_playBackInfo.m_curSelectedDeviceIndex].m_ip + " chl:" + m_playBackInfo.m_curSelectedChannelID, "Playback by time", NETDEVSDK.NETDEV_GetLastError());
                return;
            }
            showSuccessLogInfo(m_deviceInfoList[m_playBackInfo.m_curSelectedDeviceIndex].m_ip + " chl:" + m_playBackInfo.m_curSelectedChannelID, "Playback by time");
            
            m_curPlayBackPanel.m_playStatus = true;
            m_curPlayBackPanel.m_pauseStatus = false;
            m_curPlayBackPanel.m_playhandle = playBackHandle;

            playBackOpenSound();
        }

        
        private void PBCloseAllPlayBack()
        {
            m_playBackInfo.m_timer.Enabled = false;//pause timer

            
            for (int i = 0; i < arrayPlayBackPanel.Length; i++)
            {
                m_curPlayBackPanel = arrayPlayBackPanel[i];
                PBStopBtn_Click(null, null);
            }
            
            PBDownLoadStopBtn_Click(null,null);

            
            PBVideoTimeListView.Items.Clear();
            m_playBackInfo.m_findPlayBackDataList.Clear();

            m_playBackInfo.m_timer.Enabled = true;//start timer
        }

        
        private void playBackOpenSound()
        {
            int iRet = NETDEVSDK.NETDEV_OpenSound(m_curPlayBackPanel.m_playhandle);
            if (NETDEVSDK.TRUE != iRet)
            {
                showFailLogInfo(m_deviceInfoList[m_playBackInfo.m_curSelectedDeviceIndex].m_ip + " chl:" + m_playBackInfo.m_curSelectedChannelID, "Open sound fail", NETDEVSDK.NETDEV_GetLastError());
            }

            m_curPlayBackPanel.m_soundStatus = true;
            PBVolBtn.BackgroundImage = global::NetDemo.Properties.Resources.ico00008;
            PBVolBtn.Enabled = true;
            int volumeTemp = 0;
            iRet = NETDEVSDK.NETDEV_GetSoundVolume(m_curPlayBackPanel.m_playhandle, ref volumeTemp);
            if (NETDEVSDK.FALSE == iRet)
            {
                showFailLogInfo(m_deviceInfoList[m_playBackInfo.m_curSelectedDeviceIndex].m_ip + " chl:" + m_playBackInfo.m_curSelectedChannelID, "Get sound volume fail", NETDEVSDK.NETDEV_GetLastError());
                return;
            }
            showSuccessLogInfo(m_deviceInfoList[m_playBackInfo.m_curSelectedDeviceIndex].m_ip + " chl:" + m_playBackInfo.m_curSelectedChannelID, "Get sound volume");

            m_curPlayBackPanel.m_volume = volumeTemp;

            PBVolTrackBar.Value = m_curPlayBackPanel.m_volume;
            PBVolTrackBar.Enabled = true;
        }

        
        private void PBPauseBtn_Click(object sender, EventArgs e)
        {
            if (m_curPlayBackPanel.m_playStatus == true && m_curPlayBackPanel.m_pauseStatus == false)
            {
                pausePlayBack(true);
            }
        }

        
        private void PBStopBtn_Click(object sender, EventArgs e)
        {
            if (m_curPlayBackPanel.m_playStatus == true)
            {
                stopPlayBack();
            }
        }

        
        private void PBFastBackwardBtn_Click(object sender, EventArgs e)
        {
            if (m_curPlayBackPanel.m_playStatus == false || m_curPlayBackPanel.m_pauseStatus == true)
            {
                return;
            }

            long enSpeed = 0;
            if (NETDEVSDK.FALSE == NETDEVSDK.NETDEV_PlayBackControl(m_curPlayBackPanel.m_playhandle, (int)NETDEV_VOD_PLAY_CTRL_E.NETDEV_PLAY_CTRL_GETPLAYSPEED, ref enSpeed))
            {
                showFailLogInfo(m_deviceInfoList[m_playBackInfo.m_curSelectedDeviceIndex].m_ip + " chl:" + m_playBackInfo.m_curSelectedChannelID, "Get play speed", NETDEVSDK.NETDEV_GetLastError());
                return;
            }
            showSuccessLogInfo(m_deviceInfoList[m_playBackInfo.m_curSelectedDeviceIndex].m_ip + " chl:" + m_playBackInfo.m_curSelectedChannelID, "Get play speed");

            enSpeed--;

            if (enSpeed < (long)NETDEV_VOD_PLAY_STATUS_E.NETDEV_PLAY_STATUS_1_FORWARD && enSpeed > (long)NETDEV_VOD_PLAY_STATUS_E.NETDEV_PLAY_STATUS_1_BACKWARD)//5-8
            {
                enSpeed = (long)NETDEV_VOD_PLAY_STATUS_E.NETDEV_PLAY_STATUS_1_BACKWARD;
            }
            else if (enSpeed < (long)NETDEV_VOD_PLAY_STATUS_E.NETDEV_PLAY_STATUS_16_BACKWARD)
            {
                enSpeed++;
            }

            m_curPlayBackPanel.m_playSpeed = (int)enSpeed;
            if (NETDEVSDK.FALSE == NETDEVSDK.NETDEV_PlayBackControl(m_curPlayBackPanel.m_playhandle, (int)NETDEV_VOD_PLAY_CTRL_E.NETDEV_PLAY_CTRL_SETPLAYSPEED, ref enSpeed))
            {
                showFailLogInfo(m_deviceInfoList[m_playBackInfo.m_curSelectedDeviceIndex].m_ip + " chl:" + m_playBackInfo.m_curSelectedChannelID, "Set play speed", NETDEVSDK.NETDEV_GetLastError());
                return;
            }
            showSuccessLogInfo(m_deviceInfoList[m_playBackInfo.m_curSelectedDeviceIndex].m_ip + " chl:" + m_playBackInfo.m_curSelectedChannelID, "Set play speed");

            if (enSpeed < (long)NETDEV_VOD_PLAY_STATUS_E.NETDEV_PLAY_STATUS_1_FORWARD)
            {
                PBShowFBSpeedLabel.Text = "-" + Convert.ToString(Math.Pow(2, Math.Abs(enSpeed - (int)NETDEV_VOD_PLAY_STATUS_E.NETDEV_PLAY_STATUS_1_BACKWARD))) + "x";
            }
            else
            {
                PBShowFBSpeedLabel.Text = Convert.ToString(Math.Pow(2, enSpeed - (int)NETDEV_VOD_PLAY_STATUS_E.NETDEV_PLAY_STATUS_1_FORWARD)) + "x";
            }
        }

        
        private void PBFastForwardBtn_Click(object sender, EventArgs e)
        {
            if (m_curPlayBackPanel.m_playStatus == false || m_curPlayBackPanel.m_pauseStatus == true)
            {
                return;
            }

            long enSpeed = 0;
            if( NETDEVSDK.FALSE == NETDEVSDK.NETDEV_PlayBackControl(m_curPlayBackPanel.m_playhandle, (int)NETDEV_VOD_PLAY_CTRL_E.NETDEV_PLAY_CTRL_GETPLAYSPEED, ref enSpeed))
            {
                showFailLogInfo(m_deviceInfoList[m_playBackInfo.m_curSelectedDeviceIndex].m_ip + " chl:" + m_playBackInfo.m_curSelectedChannelID, "Get play speed", NETDEVSDK.NETDEV_GetLastError());
                return;
            }
            showSuccessLogInfo(m_deviceInfoList[m_playBackInfo.m_curSelectedDeviceIndex].m_ip + " chl:" + m_playBackInfo.m_curSelectedChannelID, "Get play speed");

            enSpeed++;

            if (enSpeed < (long)NETDEV_VOD_PLAY_STATUS_E.NETDEV_PLAY_STATUS_1_FORWARD && enSpeed > (long)NETDEV_VOD_PLAY_STATUS_E.NETDEV_PLAY_STATUS_1_BACKWARD)//5-8
            {
                enSpeed = (long)NETDEV_VOD_PLAY_STATUS_E.NETDEV_PLAY_STATUS_1_FORWARD;
            }
            else if (enSpeed > (long)NETDEV_VOD_PLAY_STATUS_E.NETDEV_PLAY_STATUS_16_FORWARD)
            {
                enSpeed--;
            }

            m_curPlayBackPanel.m_playSpeed = (int)enSpeed;
            if (NETDEVSDK.FALSE == NETDEVSDK.NETDEV_PlayBackControl(m_curPlayBackPanel.m_playhandle, (int)NETDEV_VOD_PLAY_CTRL_E.NETDEV_PLAY_CTRL_SETPLAYSPEED, ref enSpeed))
            {
                showFailLogInfo(m_deviceInfoList[m_playBackInfo.m_curSelectedDeviceIndex].m_ip + " chl:" + m_playBackInfo.m_curSelectedChannelID, "Set play speed", NETDEVSDK.NETDEV_GetLastError());
                return;
            }
            showSuccessLogInfo(m_deviceInfoList[m_playBackInfo.m_curSelectedDeviceIndex].m_ip + " chl:" + m_playBackInfo.m_curSelectedChannelID, "Set play speed");

            if (enSpeed < (long)NETDEV_VOD_PLAY_STATUS_E.NETDEV_PLAY_STATUS_1_FORWARD)
            {
                PBShowFBSpeedLabel.Text = "-" + Convert.ToString(Math.Pow(2, Math.Abs(enSpeed - (int)NETDEV_VOD_PLAY_STATUS_E.NETDEV_PLAY_STATUS_1_BACKWARD))) + "x";
            }
            else
            {
                PBShowFBSpeedLabel.Text = Convert.ToString(Math.Pow(2, enSpeed - (int)NETDEV_VOD_PLAY_STATUS_E.NETDEV_PLAY_STATUS_1_FORWARD)) + "x";
            }
        }

        
        private void PBVideoTrackBar_Scroll(object sender, EventArgs e)
        {
            if (m_curPlayBackPanel.m_playhandle == IntPtr.Zero)
            {
                return;
            }

            long curTime = PBVideoTrackBar.Value + m_curPlayBackPanel.m_startTime;
            if (NETDEVSDK.FALSE == NETDEVSDK.NETDEV_PlayBackControl(m_curPlayBackPanel.m_playhandle, (int)NETDEV_VOD_PLAY_CTRL_E.NETDEV_PLAY_CTRL_SETPLAYTIME, ref curTime))
            {
                showFailLogInfo(m_deviceInfoList[m_playBackInfo.m_curSelectedDeviceIndex].m_ip + " chl:" + m_playBackInfo.m_curSelectedChannelID, "Set play time", NETDEVSDK.NETDEV_GetLastError());
                return;
            }
            m_curPlayBackPanel.m_pauseStatus = false;
            showSuccessLogInfo(m_deviceInfoList[m_playBackInfo.m_curSelectedDeviceIndex].m_ip + " chl:" + m_playBackInfo.m_curSelectedChannelID, "Set play time");

        }

        
        private void PBCaptureBtn_Click(object sender, EventArgs e)
        {
            if (m_curPlayBackPanel.m_playhandle == IntPtr.Zero)
            {
                return;
            }

            String temp = string.Copy(LocalSetting.m_strPicSavePath);
            DateTime date = DateTime.Now;
            String curTime = date.ToString("yyMMddHHmmss", DateTimeFormatInfo.InvariantInfo);
            LocalSetting.m_strPicSavePath += "\\";
            LocalSetting.m_strPicSavePath += m_deviceInfoList[m_playBackInfo.m_curSelectedDeviceIndex].m_ip;
            LocalSetting.m_strPicSavePath += "_";
            LocalSetting.m_strPicSavePath += m_playBackInfo.m_curSelectedChannelID;
            LocalSetting.m_strPicSavePath += "_";
            LocalSetting.m_strPicSavePath += curTime;

            byte[] picSavePath;
            GetUTF8Buffer(LocalSetting.m_strPicSavePath, NETDEVSDK.NETDEV_LEN_260, out picSavePath);
            int iRet = NETDEVSDK.NETDEV_CapturePicture(m_curPlayBackPanel.m_playhandle, picSavePath, (int)NETDEV_PICTURE_FORMAT_E.NETDEV_PICTURE_BMP);
            if (NETDEVSDK.FALSE == iRet)
            {
                showFailLogInfo(m_deviceInfoList[m_playBackInfo.m_curSelectedDeviceIndex].m_ip + " chl:" + m_playBackInfo.m_curSelectedChannelID, "playBack Capture Picture", NETDEVSDK.NETDEV_GetLastError());
                //return;
            }
            else
            {
                showSuccessLogInfo(m_deviceInfoList[m_playBackInfo.m_curSelectedDeviceIndex].m_ip + " chl:" + m_playBackInfo.m_curSelectedChannelID, "playBack Capture Picture"); 
            }

            LocalSetting.m_strPicSavePath = temp;
        }

        
        private void startTimer()
        {
            m_playBackInfo.m_timer.AutoReset = true;
            m_playBackInfo.m_timer.Enabled = true;//start timer
        }

        
        private void timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (m_curPlayBackPanel.m_playStatus == true)
            {
                Int64 curTime = 0;
                if(NETDEVSDK.FALSE == NETDEVSDK.NETDEV_PlayBackControl(m_curPlayBackPanel.m_playhandle, (int)NETDEV_VOD_PLAY_CTRL_E.NETDEV_PLAY_CTRL_GETPLAYTIME, ref curTime))
                {
                    showFailLogInfo(m_deviceInfoList[m_playBackInfo.m_curSelectedDeviceIndex].m_ip + " chl:" + m_playBackInfo.m_curSelectedChannelID, "get play time", NETDEVSDK.NETDEV_GetLastError());
                    return;
                }

                m_curPlayBackPanel.m_curVideoSliderValue = (int)(curTime - m_curPlayBackPanel.m_startTime);
                PBVideoTrackBar.SetRange(0, m_curPlayBackPanel.m_maxVideoSliderValue);
                if (m_curPlayBackPanel.m_curVideoSliderValue >= m_curPlayBackPanel.m_maxVideoSliderValue || m_curPlayBackPanel.m_curVideoSliderValue < 0)//
                {
                    this.stopPlayBack();
                }
                else
                {
                    PBVideoTrackBar.Value = m_curPlayBackPanel.m_curVideoSliderValue;
                    PBVideoDateTimeLabel.Text = getStrTime(curTime);
                    PBRemainingTimeLabel.Text = convertRemainTime(m_curPlayBackPanel.m_endTime - curTime);
                }
            }

            // updatedownload porcess
            if (NETDEMO.NETDEMO_DOWNLOAD_TIMER_MUX_FLAG == true || NETDEMO.NETDEMO_DOWNLOAD_TIMER_STOP_ALL == true)
            {
                return;
            }

            for (int i = 0; i < m_downloadInfoList.Count; i++)
            {
                if (m_downloadInfoList[i].downLoad_status == true)
                {
                    long iPlayTime = 0;
                    int iRet = NETDEVSDK.NETDEV_PlayBackControl(m_downloadInfoList[i].lpHandle, (int)NETDEV_VOD_PLAY_CTRL_E.NETDEV_PLAY_CTRL_GETPLAYTIME, ref iPlayTime);
                    if (NETDEVSDK.TRUE != iRet)
                    {
                        //NETDEMO_LOG_ERROR(NULL, "play back control get play time");
                    }

                    if (NETDEMO.NETDEMO_DOWNLOAD_TIME_COUNT < m_downloadInfoList[i].dwCount || iPlayTime >= m_downloadInfoList[i].tEndTime)
                    {
                        NETDEVSDK.NETDEV_StopGetFile(m_downloadInfoList[i].lpHandle);
                        m_downloadInfoList[i].downLoad_status = false;
                        //m_downloadInfoList.Remove(m_downloadInfoList[i]);
                        //update porcess 100%
                        if (m_downloadInfoList.Count < m_downloadInfo.getListViewItemCount())
                        {
                            m_downloadInfo.updateProgress(i + m_downloadInfo.getListViewItemLastCount(), 100);
                        }
                        else
                        {
                            m_downloadInfo.updateProgress(i, 100);
                        }
                    }
                    else
                    {

                        if (m_downloadInfoList[i].tCurTime == iPlayTime)
                        {
                            m_downloadInfoList[i].dwCount++;
                        }
                        else
                        {
                            m_downloadInfoList[i].dwCount = 0;
                            m_downloadInfoList[i].tCurTime = iPlayTime;
                            //update porcess
                            if (m_downloadInfoList.Count < m_downloadInfo.getListViewItemCount())
                            {
                                m_downloadInfo.updateProgress(i + m_downloadInfo.getListViewItemLastCount(), (int)(((float)(iPlayTime - m_downloadInfoList[i].tBeginTime) / (m_downloadInfoList[i].tEndTime - m_downloadInfoList[i].tBeginTime)) * 100));
                            }
                            else
                            {
                                m_downloadInfo.updateProgress(i, (int)(((float)(iPlayTime - m_downloadInfoList[i].tBeginTime) / (m_downloadInfoList[i].tEndTime - m_downloadInfoList[i].tBeginTime)) * 100));
                            }
                        }
                    }
                }
            }

            NETDEMO.NETDEMO_DOWNLOAD_TIMER_MUX_FLAG = false;
        }

        
        private void stopPlayBack()
        {
            if (m_curPlayBackPanel.m_playhandle == IntPtr.Zero)
            {
                return;
            }

            if (NETDEVSDK.FALSE == NETDEVSDK.NETDEV_StopPlayBack(m_curPlayBackPanel.m_playhandle))
            {
                showFailLogInfo(m_deviceInfoList[m_playBackInfo.m_curSelectedDeviceIndex].m_ip + " chl:" + m_playBackInfo.m_curSelectedChannelID, "close playBack", NETDEVSDK.NETDEV_GetLastError());
                return;
            }
            showSuccessLogInfo(m_deviceInfoList[m_playBackInfo.m_curSelectedDeviceIndex].m_ip + " chl:" + m_playBackInfo.m_curSelectedChannelID, "close playBack");

            m_curPlayBackPanel.initPlayPanel();

            PBVideoTrackBar.Value = 0;
            PBVideoDateTimeLabel.Text = "0000/00/00 00:00:00";
            PBRemainingTimeLabel.Text = "00:00:00";
            PBVolTrackBar.Value = 0;
            PBVolTrackBar.Enabled = false;
            PBVolBtn.BackgroundImage = global::NetDemo.Properties.Resources.ico00009;
            PBVolBtn.Enabled = false;
        }

        
        private void pausePlayBack(bool flag)
        {
            if (m_curPlayBackPanel.m_playhandle == IntPtr.Zero)
            {
                return;
            }

            if (flag == true)
            {
                long temp = 0;
                if (NETDEVSDK.FALSE == NETDEVSDK.NETDEV_PlayBackControl(m_curPlayBackPanel.m_playhandle, (int)NETDEV_VOD_PLAY_CTRL_E.NETDEV_PLAY_CTRL_PAUSE, ref temp))
                {
                    showFailLogInfo(m_deviceInfoList[m_playBackInfo.m_curSelectedDeviceIndex].m_ip + " chl:" + m_playBackInfo.m_curSelectedChannelID, "pause playBack", NETDEVSDK.NETDEV_GetLastError());
                    return;
                }
                showSuccessLogInfo(m_deviceInfoList[m_playBackInfo.m_curSelectedDeviceIndex].m_ip + " chl:" + m_playBackInfo.m_curSelectedChannelID, "pause playBack");

                m_curPlayBackPanel.m_pauseStatus = true;
            }
            else
            {
                long temp = 0;
                if (NETDEVSDK.FALSE == NETDEVSDK.NETDEV_PlayBackControl(m_curPlayBackPanel.m_playhandle, (int)NETDEV_VOD_PLAY_CTRL_E.NETDEV_PLAY_CTRL_RESUME, ref temp))
                {
                    showFailLogInfo(m_deviceInfoList[m_playBackInfo.m_curSelectedDeviceIndex].m_ip + " chl:" + m_playBackInfo.m_curSelectedChannelID, "resume playBack fail", NETDEVSDK.NETDEV_GetLastError());
                    return;
                }
                showSuccessLogInfo(m_deviceInfoList[m_playBackInfo.m_curSelectedDeviceIndex].m_ip + " chl:" + m_playBackInfo.m_curSelectedChannelID, "resume playBack");

                m_curPlayBackPanel.m_pauseStatus = false;
            }
        }

        private void playBackPanel_Click(object sender, EventArgs e)
        {
            this.m_curPlayBackPanel = sender as PlayPanel;
            //this.m_playBackInfo.m_nextPlayBackPanelIndex = m_curPlayBackPanel.m_panelIndex + 1;
            this.m_playBackInfo.m_nextPlayBackPanelIndex = m_curPlayBackPanel.m_panelIndex;
            if (m_playBackInfo.m_nextPlayBackPanelIndex == arrayPlayBackPanel.Length)
            {
                m_playBackInfo.m_nextPlayBackPanelIndex = 0;
            }

            updatePlayBackControl();
            this.setPlayBackPanelBorderColor();
        }

        private void updatePlayBackControl()
        {
            if (m_curPlayBackPanel.m_playStatus == false)
            {
                PBVideoTrackBar.Value = 0;
                PBVolTrackBar.Value = 0;
                PBVideoDateTimeLabel.Text = "0000/00/00 00:00:00";
                PBRemainingTimeLabel.Text = "00:00:00";
                PBVolBtn.BackgroundImage = global::NetDemo.Properties.Resources.ico00009;
                PBVolBtn.Enabled = false;
                PBVolTrackBar.Enabled = false;
                PBShowFBSpeedLabel.Text = "1x";
            }
            else
            {
                PBVolBtn.Enabled = true;
                PBVolTrackBar.Enabled = true;
                PBVolTrackBar.Value = m_curPlayBackPanel.m_volume;
                if (m_curPlayBackPanel.m_soundStatus == true)
                {
                    PBVolBtn.BackgroundImage = global::NetDemo.Properties.Resources.ico00008;
                }
                else
                {
                    PBVolBtn.BackgroundImage = global::NetDemo.Properties.Resources.ico00009;
                }

                if (m_curPlayBackPanel.m_playSpeed < (int)NETDEV_VOD_PLAY_STATUS_E.NETDEV_PLAY_STATUS_1_FORWARD)
                {
                    PBShowFBSpeedLabel.Text = "-" + Convert.ToString(Math.Pow(2, Math.Abs(m_curPlayBackPanel.m_playSpeed - (int)NETDEV_VOD_PLAY_STATUS_E.NETDEV_PLAY_STATUS_1_BACKWARD))) + "x";
                }
                else
                {
                    PBShowFBSpeedLabel.Text = Convert.ToString(Math.Pow(2, m_curPlayBackPanel.m_playSpeed - (int)NETDEV_VOD_PLAY_STATUS_E.NETDEV_PLAY_STATUS_1_FORWARD)) + "x";
                }

            }
        }

        private void playBackPanel_DoubleClick(object sender, EventArgs e)
        {
            this.m_curPlayBackPanel = sender as PlayPanel;
            this.playBackLayoutPanel.Controls.Clear();
            if (playBackMaxFlag == true)
            {
                int nSqrt = (int)Math.Sqrt(NETDEMO.PLAYBACK_PANEL_MAX_SIZE);
                int nHeight = this.playBackLayoutPanel.Height / nSqrt - 5;
                int nWidth = this.playBackLayoutPanel.Width / nSqrt - 5;
                for (int i = 0; i < NETDEMO.PLAYBACK_PANEL_MAX_SIZE; i++)
                {
                    this.arrayPlayBackPanel[i].Height = nHeight;
                    this.arrayPlayBackPanel[i].Width = nWidth;
                    this.playBackLayoutPanel.Controls.Add(this.arrayPlayBackPanel[i]);
                }
                playBackMaxFlag = false;
            }
            else
            {
                this.m_curPlayBackPanel.Height = this.playBackLayoutPanel.Height;
                this.m_curPlayBackPanel.Width = this.playBackLayoutPanel.Width;
                this.playBackLayoutPanel.Controls.Add(this.m_curPlayBackPanel);
                playBackMaxFlag = true;
            }
        }

        public void setPlayBackPanelBorderColor()
        {
            for (int i = 0; i < NETDEMO.PLAYBACK_PANEL_MAX_SIZE; i++)
            {
                arrayPlayBackPanel[i].setBorderColor(Color.White, 1);
                arrayPlayBackPanel[i].Invalidate();
            }
            m_curPlayBackPanel.setBorderColor(Color.Red, 2);
            m_curPlayBackPanel.Invalidate();
        }

        
        private void PBRestartBtn_Click(object sender, EventArgs e)
        {
            if (m_curPlayBackPanel.m_playhandle == IntPtr.Zero)
            {
                return;
            }

            this.PBVideoTrackBar.Value = 0;
            PBStopBtn_Click(null,null);

            if (m_curPlayBackPanel.m_playStatus == true && m_curPlayBackPanel.m_pauseStatus == true)
            {
                pausePlayBack(false);
                return;
            }
            m_curPlayBackPanel = arrayPlayBackPanel[m_playBackInfo.m_nextPlayBackPanelIndex];
            m_curPlayBackPanel.m_panelIndex = m_playBackInfo.m_nextPlayBackPanelIndex;
            //m_playBackInfo.m_nextPlayBackPanelIndex++;
            this.setPlayBackPanelBorderColor();

            if (m_curPlayBackPanel.m_playStatus == true)
            {
                stopPlayBack();
            }

            if (m_playBackInfo.m_nextPlayBackPanelIndex == arrayPlayBackPanel.Length)
            {
                m_playBackInfo.m_nextPlayBackPanelIndex = 0;
            }

            NETDEV_PLAYBACKCOND_S playBackByTimeInfo = new NETDEV_PLAYBACKCOND_S();

            String beginDateTimeStr = getInputStartDataTime();
            String endDateTimeStr = getInputEndDataTime();

            playBackByTimeInfo.tBeginTime = this.getLongTime(beginDateTimeStr);
            playBackByTimeInfo.tEndTime = this.getLongTime(endDateTimeStr);

            playBackByTimeInfo.dwChannelID = m_playBackInfo.m_curSelectedChannelID;
            playBackByTimeInfo.dwLinkMode = (int)NETDEV_PROTOCAL_E.NETDEV_TRANSPROTOCAL_RTPTCP;
            playBackByTimeInfo.dwPlaySpeed = (int)NETDEV_VOD_PLAY_STATUS_E.NETDEV_PLAY_STATUS_1_FORWARD;
            playBackByTimeInfo.hPlayWnd = m_curPlayBackPanel.Handle;
            m_curPlayBackPanel.m_maxVideoSliderValue = (int)(playBackByTimeInfo.tEndTime - playBackByTimeInfo.tBeginTime);
            m_curPlayBackPanel.m_startTime = playBackByTimeInfo.tBeginTime;
            m_curPlayBackPanel.m_endTime = playBackByTimeInfo.tEndTime;

            PBVideoTrackBar.SetRange(0, m_curPlayBackPanel.m_maxVideoSliderValue);

            IntPtr playBackHandle = NETDEVSDK.NETDEV_PlayBackByTime(m_playBackInfo.m_devHandle, ref playBackByTimeInfo);
            if (playBackHandle == IntPtr.Zero)
            {
                showFailLogInfo(m_deviceInfoList[m_playBackInfo.m_curSelectedDeviceIndex].m_ip + " chl:" + m_playBackInfo.m_curSelectedChannelID, "reset Playback by time", NETDEVSDK.NETDEV_GetLastError());
                return;
            }
            showSuccessLogInfo(m_deviceInfoList[m_playBackInfo.m_curSelectedDeviceIndex].m_ip + " chl:" + m_playBackInfo.m_curSelectedChannelID, "reset Playback by time");

            m_curPlayBackPanel.m_playStatus = true;
            m_curPlayBackPanel.m_pauseStatus = false;
            m_curPlayBackPanel.m_playhandle = playBackHandle;

            playBackOpenSound();

            startTimer();
        }

        
        private void PBFrameBtn_Click(object sender, EventArgs e)
        {
            //if(m_curPlayBackPanel)
            long enSpeed = (long)NETDEV_VOD_PLAY_STATUS_E.NETDEV_PLAY_STATUS_1_FRAME_FORWD;
            int iRet = NETDEVSDK.NETDEV_PlayBackControl(m_curPlayBackPanel.m_playhandle, (int)NETDEV_VOD_PLAY_CTRL_E.NETDEV_PLAY_CTRL_SINGLE_FRAME, ref enSpeed);
            if (NETDEVSDK.TRUE != iRet)
            {
                showFailLogInfo(m_deviceInfoList[m_playBackInfo.m_curSelectedDeviceIndex].m_ip + " chl:" + m_playBackInfo.m_curSelectedChannelID, "Playback control single frame fail", NETDEVSDK.NETDEV_GetLastError());
            }
            else
            {
                showSuccessLogInfo(m_deviceInfoList[m_playBackInfo.m_curSelectedDeviceIndex].m_ip + " chl:" + m_playBackInfo.m_curSelectedChannelID, "Playback control single frame");                
            }
        }

        
        private void PBDownLoadStartBtn_Click(object sender, EventArgs e)
        {
            if (PBVideoTimeListView.SelectedItems.Count == 0)
            {
                return;
            }

            NETDEV_PLAYBACKCOND_S stPlayBackInfo = new NETDEV_PLAYBACKCOND_S();
            String beginDateTimeStr = getInputStartDataTime();
            String endDateTimeStr = getInputEndDataTime();

            stPlayBackInfo.tBeginTime = this.getLongTime(beginDateTimeStr);
            stPlayBackInfo.tEndTime = this.getLongTime(endDateTimeStr);

            stPlayBackInfo.hPlayWnd = IntPtr.Zero;
            stPlayBackInfo.dwDownloadSpeed = (int)NETDEV_E_DOWNLOAD_SPEED_E.NETDEV_DOWNLOAD_SPEED_EIGHT;
            stPlayBackInfo.dwChannelID = m_playBackInfo.m_curSelectedChannelID;

            IntPtr lpDevHandle = m_playBackInfo.m_devHandle;
            if (IntPtr.Zero == lpDevHandle)
            {
                return;
            }

            String temp = string.Copy(LocalSetting.m_strLocalRecordPath);
            DateTime date = DateTime.Now;
            String curTime = date.ToString("yyyyMMddHHmmss", DateTimeFormatInfo.InvariantInfo);
            LocalSetting.m_strLocalRecordPath += "\\";
            LocalSetting.m_strLocalRecordPath += m_deviceInfoList[m_playBackInfo.m_curSelectedDeviceIndex].m_ip;
            LocalSetting.m_strLocalRecordPath += "_";
            LocalSetting.m_strLocalRecordPath += m_playBackInfo.m_curSelectedChannelID;
            LocalSetting.m_strLocalRecordPath += "_";
            LocalSetting.m_strLocalRecordPath += curTime;

            byte[] localRecordPath;
            GetUTF8Buffer(LocalSetting.m_strLocalRecordPath, NETDEVSDK.NETDEV_LEN_260, out localRecordPath);
            IntPtr pHandle = NETDEVSDK.NETDEV_GetFileByTime(lpDevHandle, ref stPlayBackInfo, localRecordPath, (int)NETDEV_MEDIA_FILE_FORMAT_E.NETDEV_MEDIA_FILE_MP4);
            if (IntPtr.Zero == pHandle)
            {
                showFailLogInfo(m_deviceInfoList[m_playBackInfo.m_curSelectedDeviceIndex].m_ip + " chl:" + m_playBackInfo.m_curSelectedChannelID, "Get file by time", NETDEVSDK.NETDEV_GetLastError());
            }
            else
            {
                showSuccessLogInfo(m_deviceInfoList[m_playBackInfo.m_curSelectedDeviceIndex].m_ip + " chl:" + m_playBackInfo.m_curSelectedChannelID, "Get file by time");
                String strOut;
                strOut = "Download succeed, Path: ";
                strOut += LocalSetting.m_strLocalRecordPath;
                NETDEMO.NETDEMO_UPDATE_TIME_INFO stUpdateInfo = new NETDEMO.NETDEMO_UPDATE_TIME_INFO();
                stUpdateInfo.lpHandle = pHandle;
                stUpdateInfo.tBeginTime = stPlayBackInfo.tBeginTime;
                stUpdateInfo.tEndTime = stPlayBackInfo.tEndTime;
                stUpdateInfo.strFileName = LocalSetting.m_strLocalRecordPath;
                stUpdateInfo.strFilePath = temp;
                stUpdateInfo.dwCount = 0;
                stUpdateInfo.tCurTime = 0;
                stUpdateInfo.downLoad_status = true;

                m_downloadInfoList.Add(stUpdateInfo);
                m_downloadInfo.setListView(stUpdateInfo);
                NETDEMO.NETDEMO_DOWNLOAD_TIMER_STOP_ALL = false;
                MessageBox.Show(strOut, "Download");
            }

            LocalSetting.m_strLocalRecordPath = temp;
            return;
        }

        
        private void PBDownLoadInfoBtn_Click(object sender, EventArgs e)
        {
            this.m_downloadInfo.Show();
        }

        
        private void PBDownLoadStopBtn_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < m_downloadInfoList.Count;i++)
            {
                if (m_downloadInfoList[i].downLoad_status == false)
                {
                    continue;
                }

                m_downloadInfoList[i].downLoad_status = false;
                NETDEMO.NETDEMO_DOWNLOAD_TIMER_STOP_ALL = true;

                int iRet = NETDEVSDK.NETDEV_StopGetFile(m_downloadInfoList[i].lpHandle);
                if (NETDEVSDK.TRUE != iRet)
                {
                    showFailLogInfo(m_deviceInfoList[m_playBackInfo.m_curSelectedDeviceIndex].m_ip + " chl:" + m_playBackInfo.m_curSelectedChannelID, "Stop get file fail", NETDEVSDK.NETDEV_GetLastError());
                }
                else
                {
                    showSuccessLogInfo(m_deviceInfoList[m_playBackInfo.m_curSelectedDeviceIndex].m_ip + " chl:" + m_playBackInfo.m_curSelectedChannelID, "Stop get file");                    
                }
            }

            int count = m_downloadInfo.getListViewItemCount();
            for (int j = 0; j < count; j++)
            {
                m_downloadInfo.updateProgress(j, 100);
            }
            m_downloadInfo.setListViewItemLastCount();
            m_downloadInfoList.Clear();
        }

        /* vol */
        private void PBVolBtn_Click(object sender, EventArgs e)
        {
            if(m_curPlayBackPanel.m_playhandle == IntPtr.Zero)
            {
                return;
            }

            if (m_curPlayBackPanel.m_soundStatus == false)
            {
                playBackOpenSound();
            }
            else
            {
                int iRet = NETDEVSDK.NETDEV_CloseSound(m_curPlayBackPanel.m_playhandle);
                if (NETDEVSDK.TRUE != iRet)
                {
                    showFailLogInfo(m_deviceInfoList[m_playBackInfo.m_curSelectedDeviceIndex].m_ip + " chl:" + m_playBackInfo.m_curSelectedChannelID, "Close sound", NETDEVSDK.NETDEV_GetLastError());
                }
                else
                {
                    showSuccessLogInfo(m_deviceInfoList[m_playBackInfo.m_curSelectedDeviceIndex].m_ip + " chl:" + m_playBackInfo.m_curSelectedChannelID, "Close sound");                    
                }

                m_curPlayBackPanel.m_soundStatus = false;
                PBVolBtn.BackgroundImage = global::NetDemo.Properties.Resources.ico00009;
            }
        }


        private void PBVolTrackBar_Scroll(object sender, EventArgs e)
        {
            if (m_curPlayBackPanel == null || m_curPlayBackPanel.m_playhandle == IntPtr.Zero)
            {
                return;
            }

            int iRet = NETDEVSDK.NETDEV_SoundVolumeControl(m_curPlayBackPanel.m_playhandle, (sender as TrackBar).Value);
            if (NETDEVSDK.TRUE != iRet)
            {
                showFailLogInfo(m_deviceInfoList[m_playBackInfo.m_curSelectedDeviceIndex].m_ip + " chl:" + m_playBackInfo.m_curSelectedChannelID, "Sound volume control", NETDEVSDK.NETDEV_GetLastError());
            }
            else
            {
                showSuccessLogInfo(m_deviceInfoList[m_playBackInfo.m_curSelectedDeviceIndex].m_ip + " chl:" + m_playBackInfo.m_curSelectedChannelID, "Sound volume control");                
            }
            m_curPlayBackPanel.m_volume = (sender as TrackBar).Value;
        }

        private void cfgTabControl_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (m_CurSelectTreeNodeInfo.dwDeviceIndex >= 0 && m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_lpDevHandle != IntPtr.Zero)
            {
                m_config.cfgTabSwitch((sender as TabControl).SelectedIndex);
            }
        }

        public void showSystemTime(NETDEV_TIME_CFG_S stTimeCfg)
        {
            this.BasicGMTCobBox.SelectedIndex = (int)stTimeCfg.dwTimeZone;

            this.BasicDate.Text = Convert.ToString(stTimeCfg.stTime.dwYear) + "/" + Convert.ToString(stTimeCfg.stTime.dwMonth) + "/" + Convert.ToString(stTimeCfg.stTime.dwDay);
            this.BasicTime.Text = Convert.ToString(stTimeCfg.stTime.dwHour) + ":" + Convert.ToString(stTimeCfg.stTime.dwMinute) + ":" + Convert.ToString(stTimeCfg.stTime.dwSecond);
        }

        public void showDeviceName(string deviceName)
        {
            BasicDeviceNameText.Text = deviceName;
        }

        public void showDiskInfoList(NETDEV_DISK_INFO_LIST_S stDiskInfoList)
        {
            this.BasicHDInfoListView.Items.Clear();
            for (int i = 0; i < stDiskInfoList.dwSize; i++)
            {
                ListViewItem item = new ListViewItem(Convert.ToString(stDiskInfoList.astDisksInfo[i].dwSlotIndex));
                item.SubItems.Add(Convert.ToString(stDiskInfoList.astDisksInfo[i].dwTotalCapacity / 1024));
                item.SubItems.Add(Convert.ToString((stDiskInfoList.astDisksInfo[i].dwTotalCapacity - stDiskInfoList.astDisksInfo[i].dwUsedCapacity) / 1024));

                String str;
                switch (stDiskInfoList.astDisksInfo[i].enStatus)
                {
                    case NETDEV_DISK_WORK_STATUS_E.NETDEV_DISK_WORK_STATUS_EMPTY:             /*  Empty/No Disk */
                        str = "No Disk";
                        break;
                    case NETDEV_DISK_WORK_STATUS_E.NETDEV_DISK_WORK_STATUS_UNFORMAT:          /*  Unformat */
                        str = "Unformat";
                        break;
                    case NETDEV_DISK_WORK_STATUS_E.NETDEV_DISK_WORK_STATUS_FORMATING:         /*  Formating */
                        str = "Formating";
                        break;
                    case NETDEV_DISK_WORK_STATUS_E.NETDEV_DISK_WORK_STATUS_RUNNING:           /*  Running/Normal */
                        str = "Normal";
                        break;
                    case NETDEV_DISK_WORK_STATUS_E.NETDEV_DISK_WORK_STATUS_HIBERNATE:         /*  Hibernate */
                        str = "Hibernate";
                        break;
                    case NETDEV_DISK_WORK_STATUS_E.NETDEV_DISK_WORK_STATUS_ABNORMAL:          /*  Abnormal */
                        str = "Abnormal";
                        break;
                    default:                                        /*  Unknown */
                        str = "Unknown";
                        break;
                }
                item.SubItems.Add(str);
                item.SubItems.Add(stDiskInfoList.astDisksInfo[i].szManufacturer);
                this.BasicHDInfoListView.Items.Add(item);
            }
        }

        private void BasicSysTimeSaveBtn_Click(object sender, EventArgs e)
        {
            if (m_CurSelectTreeNodeInfo.dwDeviceIndex >= 0 && m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_lpDevHandle != IntPtr.Zero)
            {
                m_config.saveStstemTime(this.BasicGMTCobBox.SelectedIndex, this.BasicDate.Text, this.BasicTime.Text);
            }
        }

        private void BasicDeviceNameSaveBtn_Click(object sender, EventArgs e)
        {
            if (m_CurSelectTreeNodeInfo.dwDeviceIndex >= 0 && m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_lpDevHandle != IntPtr.Zero)
            {
                m_config.saveDeviceName(BasicDeviceNameText.Text);
            }
        }

        private void BaiscRefreshBtn_Click(object sender, EventArgs e)
        {
            if (m_CurSelectTreeNodeInfo.dwDeviceIndex >= 0 && m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_lpDevHandle != IntPtr.Zero)
            {
                m_config.refreshBasicInfo();
            }
        }

        // network Info
        private void NetSaveBtn_Click(object sender, EventArgs e)
        {
            NETDEV_NETWORKCFG_S stNetworkSetcfg = new NETDEV_NETWORKCFG_S();

            /* IP address */
            if ("" != this.NetIPAddText.Text)
            {
                stNetworkSetcfg.Ipv4AddressStr = NetIPAddText.Text;
            }

            /* Gate way */
            if ("" != NetGatwayText.Text)
            {
                stNetworkSetcfg.szIPv4GateWay = NetGatwayText.Text;
            }

            /* sub netmask */
            if ("" != NetSubMaskText.Text)
            {
                stNetworkSetcfg.szIPv4SubnetMask = NetSubMaskText.Text;
            }

            /* MTU */
            stNetworkSetcfg.dwMTU = Convert.ToInt32(NetMTUText.Text);

            /* DHCP */
            if (NetDHCPCkBox.Checked == true)
            {
                stNetworkSetcfg.dwIPv4DHCP = NETDEVSDK.TRUE;
            }
            else
            {
                stNetworkSetcfg.dwIPv4DHCP = NETDEVSDK.FALSE;
            }

            if (m_CurSelectTreeNodeInfo.dwDeviceIndex >= 0 && m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_lpDevHandle != IntPtr.Zero)
            {
                m_config.saveBaseNetworkInfo(stNetworkSetcfg);
            }
        }

        // network Info
        private void NetPortSaveBtn_Click(object sender, EventArgs e)
        {

            NETDEV_UPNP_NAT_STATE_S stNatState = new NETDEV_UPNP_NAT_STATE_S();
            stNatState.astUpnpPort = new NETDEV_UPNP_PORT_STATE_S[NETDEVSDK.NETDEV_LEN_16];

            stNatState.dwSize = 3;

            stNatState.astUpnpPort[0].dwPort = Convert.ToInt32(NetPortHTTPText.Text);
            stNatState.astUpnpPort[0].eType = NETDEV_PROTOCOL_TYPE_E.NETDEV_PROTOCOL_TYPE_HTTP;
            stNatState.astUpnpPort[0].bEnbale = NetPortHTTPCobBox.SelectedIndex;


            stNatState.astUpnpPort[1].dwPort = Convert.ToInt32(NetPortHTTPSText.Text);
            stNatState.astUpnpPort[1].eType = NETDEV_PROTOCOL_TYPE_E.NETDEV_PROTOCOL_TYPE_HTTPS;
            stNatState.astUpnpPort[1].bEnbale = NetPortHTTPSCobBox.SelectedIndex;


            stNatState.astUpnpPort[2].dwPort = Convert.ToInt32(NetPortRTSPText.Text);
            stNatState.astUpnpPort[2].eType = NETDEV_PROTOCOL_TYPE_E.NETDEV_PROTOCOL_TYPE_RTSP;
            stNatState.astUpnpPort[2].bEnbale = NetPortRTSPCobBox.SelectedIndex;
            if (m_CurSelectTreeNodeInfo.dwDeviceIndex >= 0 && m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_lpDevHandle != IntPtr.Zero)
            {
                m_config.savePortNetworkInfo(stNatState);
            }
        }

        /* network Info
         * NTP
         */
        private void NetNTPSaveBtn_Click(object sender, EventArgs e)
        {
            NETDEV_SYSTEM_NTP_INFO_S stNTPInfo = new NETDEV_SYSTEM_NTP_INFO_S();

            if (NetNTPDHCPCkBox.CheckState == CheckState.Checked)
            {
                stNTPInfo.bSupportDHCP = NETDEVSDK.TRUE;
            }
            else
            {
                stNTPInfo.bSupportDHCP = NETDEVSDK.FALSE;
            }

            stNTPInfo.stAddr.eIPType = NetNTPIPTypeCobBox.SelectedIndex;
            stNTPInfo.stAddr.szIPAddr = NetNTPServerIPText.Text;

            if (m_CurSelectTreeNodeInfo.dwDeviceIndex >= 0 && m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_lpDevHandle != IntPtr.Zero)
            {
                m_config.saveNTPNetworkInfo(stNTPInfo);
            }
        }

        /* network Info
         * 
         */
        private void NetworkRefreshBtn_Click(object sender, EventArgs e)
        {
            if (m_CurSelectTreeNodeInfo.dwDeviceIndex >= 0 && m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_lpDevHandle != IntPtr.Zero)
            {
                m_config.refreshNetworkInfo();
            }
        }

        /*network Info
         * DHCP CheckedChanged
         */
        private void NetDHCPCkBox_CheckedChanged(object sender, EventArgs e)
        {
            if((sender as CheckBox).CheckState == CheckState.Checked)
            {
                DHCPEnable(false);
            }
            else
            {
                DHCPEnable(true);
            }
        }

        public void DHCPEnable(bool flag)
        {

            NetIPAddText.Enabled = flag;
            NetSubMaskText.Enabled = flag;
            NetGatwayText.Enabled = flag;
        }

        /*network Info
         * 
         */
        public void showBaseNetworkInfo(NETDEV_NETWORKCFG_S stNetworkcfg)
        {
            if(stNetworkcfg.dwIPv4DHCP == NETDEVSDK.TRUE)
            {
                this.NetDHCPCkBox.Checked = true;
            }
            else
            {
                this.NetDHCPCkBox.Checked = false;
            }

            NetIPAddText.Text = stNetworkcfg.Ipv4AddressStr;
            NetSubMaskText.Text = stNetworkcfg.szIPv4SubnetMask;
            NetGatwayText.Text = stNetworkcfg.szIPv4GateWay;
            NetMTUText.Text = Convert.ToString(stNetworkcfg.dwMTU);

            DHCPEnable(!this.NetDHCPCkBox.Checked);
        }

        /*network Info
         * port
         */
        public void showPortNetworkInfo(NETDEV_UPNP_NAT_STATE_S stNatState)
        {
            for (Int32 i = 0; i < stNatState.dwSize; i++)
            {
                switch ((int)(stNatState.astUpnpPort[i].eType))
                {
                    case (int)NETDEV_PROTOCOL_TYPE_E.NETDEV_PROTOCOL_TYPE_HTTP:
                        {
                            NetPortHTTPText.Text = Convert.ToString(stNatState.astUpnpPort[i].dwPort);
                            Int32 index = stNatState.astUpnpPort[i].bEnbale;
                            NetPortHTTPCobBox.SelectedIndex = index;
                        }
                        break;
                    case (int)NETDEV_PROTOCOL_TYPE_E.NETDEV_PROTOCOL_TYPE_HTTPS:
                        {
                            NetPortHTTPSText.Text = Convert.ToString(stNatState.astUpnpPort[i].dwPort);
                            Int32 index = stNatState.astUpnpPort[i].bEnbale;
                            NetPortHTTPSCobBox.SelectedIndex = index;
                        }
                        break;
                    case (int)NETDEV_PROTOCOL_TYPE_E.NETDEV_PROTOCOL_TYPE_RTSP:
                        {
                            NetPortRTSPText.Text = Convert.ToString(stNatState.astUpnpPort[i].dwPort);
                            Int32 index = stNatState.astUpnpPort[i].bEnbale;
                            NetPortRTSPCobBox.SelectedIndex = index;
                        }
                        break;
                    default:
                        break;
                }
            }
        }

        /*network Info
         * NTP
         */
        public void showNTPNetworkInfo(NETDEV_SYSTEM_NTP_INFO_S stNTPInfo)
        {
            /* Support DHCP */
            if (NETDEVSDK.TRUE == stNTPInfo.bSupportDHCP)
            {
                NetNTPDHCPCkBox.Checked = true;
            }
            else
            {
                NetNTPDHCPCkBox.Checked = false;
            }

            /* IP type */
            NetNTPIPTypeCobBox.SelectedIndex = stNTPInfo.stAddr.eIPType;

            /* Ntp Server IP address */
            NetNTPServerIPText.Text = stNTPInfo.stAddr.szIPAddr;


        }

        private void NetNTPDHCPCkBox_CheckedChanged(object sender, EventArgs e)
        {
            if ((sender as CheckBox).CheckState == CheckState.Checked)
            {
                NTPDHCPEnable(false);
            }
            else
            {
                NTPDHCPEnable(true);
            }
        }

        private void NTPDHCPEnable(bool flag)
        {
            NetNTPIPTypeCobBox.Enabled = flag;

            /* Ntp Server IP address */
            NetNTPServerIPText.Enabled = flag;
        }

        /*video Info
         * video
         */
        private void VideoSaveBtn_Click(object sender, EventArgs e)
        {
            int dwChannelIndex = getChannelIndex();
            int dwDeviceIndex = getDeviceIndex();

            if (dwDeviceIndex < 0 || dwChannelIndex < 0)
            {
                return;
            }

            Int32 dwIndex = VideoStreamIndexCobBox.SelectedIndex;
            if (dwIndex < 0 || dwIndex > 2)
            {
                return;
            }

            if (NETDEMO.NETDEMO_DEVICE_TYPE_E.NETDEMO_DEVICE_VMS != m_deviceInfoList[dwDeviceIndex].m_eDeviceType)
            {
                NETDEV_VIDEO_STREAM_INFO_S stStreamInfo = m_deviceInfoList[dwDeviceIndex].m_channelInfoList[dwChannelIndex].m_videoStreamInfo.videoStreamInfoList[dwIndex];

                stStreamInfo.enStreamType = (NETDEV_LIVE_STREAM_INDEX_E)dwIndex;
                stStreamInfo.dwBitRate = Convert.ToInt32(VideoBitRateText.Text);
                stStreamInfo.dwFrameRate = Convert.ToInt32(VideoFrameRateText.Text);
                stStreamInfo.dwGop = Convert.ToInt32(VideoGopText.Text);

                stStreamInfo.enQuality = (NETDEV_VIDEO_QUALITY_E)getVideoQualityEnmu(VideoQualityCobBox.SelectedIndex);
                stStreamInfo.dwHeight = Convert.ToInt32(VideoResolutionHText.Text);
                stStreamInfo.dwWidth = Convert.ToInt32(VideoResolutionWText.Text);
                stStreamInfo.enCodeType = (NETDEV_VIDEO_CODE_TYPE_E)Convert.ToInt32(VideoEncodeFormatCobBox.SelectedIndex);

                m_config.saveVideoInfo(stStreamInfo);
                m_deviceInfoList[dwDeviceIndex].m_channelInfoList[dwChannelIndex].m_videoStreamInfo.videoStreamInfoList[dwIndex] = stStreamInfo;
            }
        }

        /*video Info
         * Video
         */
        private void VideoRefreshBtn_Click(object sender, EventArgs e)
        {
            if (m_CurSelectTreeNodeInfo.dwDeviceIndex >= 0 && m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_lpDevHandle != IntPtr.Zero)
            {
                m_config.refreshVideoInfo();
            }
        }

        /*video Info
         * Stream Index SelectedIndexChanged
         */
        private void VideoStreamIndexCobBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_channelInfoList[getChannelIndex()].m_videoStreamInfo.existFlag == true)
            {
                showVideoInfo(m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_channelInfoList[getChannelIndex()].m_videoStreamInfo.videoStreamInfoList[(sender as ComboBox).SelectedIndex]);
            }
        }

        /* video Info
         * Video
         */
        public void showVideoInfo(NETDEV_VIDEO_STREAM_INFO_S videoStreamInfo)
        {
            VideoBitRateText.Text = Convert.ToString(videoStreamInfo.dwBitRate);
            VideoFrameRateText.Text = Convert.ToString(videoStreamInfo.dwFrameRate);
            VideoGopText.Text = Convert.ToString(videoStreamInfo.dwGop);
            VideoResolutionHText.Text = Convert.ToString(videoStreamInfo.dwHeight);
            VideoResolutionWText.Text = Convert.ToString(videoStreamInfo.dwWidth);
            VideoQualityCobBox.SelectedIndex = getVideoQualityComboIndex(videoStreamInfo.enQuality);
            VideoEncodeFormatCobBox.SelectedIndex = (int)videoStreamInfo.enCodeType;
            VideoStreamIndexCobBox.SelectedIndex = (int)videoStreamInfo.enStreamType;
        }

        /* video Info
         * enQualityenQuality
         */
        private int getVideoQualityComboIndex(NETDEV_VIDEO_QUALITY_E enQuality)
        {
            for (int i = 0; i < NETDEMO.gastVideoQualityMap.Length; i++)
            {
                if (enQuality == NETDEMO.gastVideoQualityMap[i])
                {
                    return i;
                }
            }
            return 0;
        }

        /* video Info
         * enQualityenQuality
         */
        private NETDEV_VIDEO_QUALITY_E getVideoQualityEnmu(int enQualityindex)
        {
            return NETDEMO.gastVideoQualityMap[enQualityindex];
        }


        /*Image Info
         * Image
         */
        private void ImageSaveBtn_Click(object sender, EventArgs e)
        {
            NETDEV_IMAGE_SETTING_S stImageInfo = new NETDEV_IMAGE_SETTING_S();
            stImageInfo.dwBrightness = BrightnessTrackBar.Value;
            stImageInfo.dwContrast = ContrastTrackBar.Value;
            stImageInfo.dwSaturation = SaturationTrackBar.Value;
            stImageInfo.dwSharpness = SharpnessTrackBar.Value;

            if (m_CurSelectTreeNodeInfo.dwDeviceIndex >= 0 && m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_lpDevHandle != IntPtr.Zero)
            {
                m_config.saveImageInfo(stImageInfo);
            }
        }

        /*Image Info
         * Image
         */
        private void ImageRefreshBtn_Click(object sender, EventArgs e)
        {
            if (m_CurSelectTreeNodeInfo.dwDeviceIndex >= 0 && m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_lpDevHandle != IntPtr.Zero)
            {
                m_config.refreshImageInfo();
            }
        }

        /* Image Info
         * Image
         */
        public void showImageInfo(NETDEV_IMAGE_SETTING_S stImageInfo)
        {
            BrightnessText.Text = Convert.ToString(stImageInfo.dwBrightness);
            ContrastText.Text = Convert.ToString(stImageInfo.dwContrast);
            SaturationText.Text = Convert.ToString(stImageInfo.dwSaturation);
            SharpnessText.Text = Convert.ToString(stImageInfo.dwSharpness);

            BrightnessTrackBar.Value = stImageInfo.dwBrightness;
            ContrastTrackBar.Value = stImageInfo.dwContrast;
            SaturationTrackBar.Value = stImageInfo.dwSaturation;
            SharpnessTrackBar.Value = stImageInfo.dwSharpness;
        }

        private void BrightnessTrackBar_Scroll(object sender, EventArgs e)
        {
            this.BrightnessText.Text = Convert.ToString((sender as TrackBar).Value);
        }

        private void SaturationTrackBar_Scroll(object sender, EventArgs e)
        {
            this.SaturationText.Text = Convert.ToString((sender as TrackBar).Value);
        }

        private void ContrastTrackBar_Scroll(object sender, EventArgs e)
        {
            this.ContrastText.Text = Convert.ToString((sender as TrackBar).Value);
        }

        private void SharpnessTrackBar_Scroll(object sender, EventArgs e)
        {
            this.SharpnessText.Text = Convert.ToString((sender as TrackBar).Value);
        }

        private void BrightnessText_TextChanged(object sender, EventArgs e)
        {
            int value = 0;
            try
            {
                value = Convert.ToInt32(this.BrightnessText.Text);
            }
            catch(FormatException)
            {
                return;
            }

            if(value < 0 || value > 255)
            {
                return;
            }

            this.BrightnessTrackBar.Value = value;
        }

        private void SaturationText_TextChanged(object sender, EventArgs e)
        {
            int value = 0;
            try
            {
                value = Convert.ToInt32(this.SaturationText.Text);
            }
            catch (FormatException)
            {
                return;
            }

            if (value < 0 || value > 255)
            {
                return;
            }

            this.SaturationTrackBar.Value = value;
        }

        private void ContrastText_TextChanged(object sender, EventArgs e)
        {
            int value = 0;
            try
            {
                value = Convert.ToInt32(this.ContrastText.Text);
            }
            catch (FormatException)
            {
                return;
            }

            if (value < 0 || value > 255)
            {
                return;
            }

            this.ContrastTrackBar.Value = value;
        }

        private void SharpnessText_TextChanged(object sender, EventArgs e)
        {
            int value = 0;
            try
            {
                value = Convert.ToInt32(this.SharpnessText.Text);
            }
            catch (FormatException)
            {
                return;
            }

            if (value < 0 || value > 255)
            {
                return;
            }

            this.SharpnessTrackBar.Value = value;
        }

        /*OSD Info
         * OSD
         */
        private void OSDSaveBtn_Click(object sender, EventArgs e)
        {
            NETDEV_VIDEO_OSD_CFG_S stOSDInfo = new NETDEV_VIDEO_OSD_CFG_S();
            stOSDInfo.astTextOverlay = new NETDEV_OSD_TEXT_OVERLAY_S[NETDEVSDK.NETDEV_OSD_TEXTOVERLAY_NUM];

            /* Time */
            stOSDInfo.stTimeOSD.bEnableFlag = 1;//Convert.ToInt32(this.OSDTimeCheckBox.Checked);
            stOSDInfo.stTimeOSD.stAreaScope.dwLocateX = Convert.ToInt32(this.OSDTimePointXText.Text);
            stOSDInfo.stTimeOSD.stAreaScope.dwLocateY = Convert.ToInt32(this.OSDTimePointYText.Text);
            stOSDInfo.stTimeOSD.udwDateFormat = (UInt32)this.OSDDateCobBox.SelectedIndex;
            stOSDInfo.stTimeOSD.udwTimeFormat = (UInt32)this.OSDTimeCobBox.SelectedIndex;

            /* NAME */
            stOSDInfo.stNameOSD.bEnableFlag = 1;//Convert.ToInt32(OSDNameCheckBox.Checked);
            GetUTF8Buffer(OSDNameText.Text, NETDEVSDK.NETDEV_OSD_TEXT_MAX_LEN, out stOSDInfo.stNameOSD.OSDText);
            stOSDInfo.stNameOSD.stAreaScope.dwLocateX = Convert.ToInt32(OSDNamePointXText.Text);
            stOSDInfo.stNameOSD.stAreaScope.dwLocateY = Convert.ToInt32(OSDNamePointYText.Text);

            /* Text */
            stOSDInfo.astTextOverlay[0].bEnableFlag = 1;// Convert.ToInt32(OSDText1CheckBox.Checked);
            GetUTF8Buffer(OSDText1.Text, NETDEVSDK.NETDEV_OSD_TEXT_MAX_LEN, out stOSDInfo.astTextOverlay[0].OSDText);
            stOSDInfo.astTextOverlay[0].stAreaScope.dwLocateX = Convert.ToInt32(OSDText1PointX.Text);
            stOSDInfo.astTextOverlay[0].stAreaScope.dwLocateY = Convert.ToInt32(OSDText1PointY.Text);

            stOSDInfo.astTextOverlay[1].bEnableFlag = 1;// Convert.ToInt32(OSDText2CheckBox.Checked);
            GetUTF8Buffer(OSDText2.Text, NETDEVSDK.NETDEV_OSD_TEXT_MAX_LEN, out stOSDInfo.astTextOverlay[1].OSDText);
            stOSDInfo.astTextOverlay[1].stAreaScope.dwLocateX = Convert.ToInt32(OSDText2PointX.Text);
            stOSDInfo.astTextOverlay[1].stAreaScope.dwLocateY = Convert.ToInt32(OSDText2PointY.Text);

            stOSDInfo.astTextOverlay[2].bEnableFlag = 1;// Convert.ToInt32(OSDText3CheckBox.Checked);
            GetUTF8Buffer(OSDText3.Text, NETDEVSDK.NETDEV_OSD_TEXT_MAX_LEN, out stOSDInfo.astTextOverlay[2].OSDText);
            stOSDInfo.astTextOverlay[2].stAreaScope.dwLocateX = Convert.ToInt32(OSDText3PointX.Text);
            stOSDInfo.astTextOverlay[2].stAreaScope.dwLocateY = Convert.ToInt32(OSDText3PointY.Text);

            stOSDInfo.astTextOverlay[3].bEnableFlag = 1;// Convert.ToInt32(OSDText4CheckBox.Checked);
            GetUTF8Buffer(OSDText4.Text, NETDEVSDK.NETDEV_OSD_TEXT_MAX_LEN, out stOSDInfo.astTextOverlay[3].OSDText);
            stOSDInfo.astTextOverlay[3].stAreaScope.dwLocateX = Convert.ToInt32(OSDText4PointX.Text);
            stOSDInfo.astTextOverlay[3].stAreaScope.dwLocateY = Convert.ToInt32(OSDText4PointY.Text);

            stOSDInfo.astTextOverlay[4].bEnableFlag = 1;// Convert.ToInt32(OSDText5CheckBox.Checked);
            GetUTF8Buffer(OSDText5.Text, NETDEVSDK.NETDEV_OSD_TEXT_MAX_LEN, out stOSDInfo.astTextOverlay[4].OSDText);
            stOSDInfo.astTextOverlay[4].stAreaScope.dwLocateX = Convert.ToInt32(OSDText5PointX.Text);
            stOSDInfo.astTextOverlay[4].stAreaScope.dwLocateY = Convert.ToInt32(OSDText5PointY.Text);

            stOSDInfo.astTextOverlay[5].bEnableFlag = 1;// Convert.ToInt32(OSDText6CheckBox.Checked);
            GetUTF8Buffer(OSDText6.Text, NETDEVSDK.NETDEV_OSD_TEXT_MAX_LEN, out stOSDInfo.astTextOverlay[5].OSDText);
            stOSDInfo.astTextOverlay[5].stAreaScope.dwLocateX = Convert.ToInt32(OSDText6PointX.Text);
            stOSDInfo.astTextOverlay[5].stAreaScope.dwLocateY = Convert.ToInt32(OSDText6PointY.Text);
            
            stOSDInfo.wTextNum = 6;
            if (m_CurSelectTreeNodeInfo.dwDeviceIndex >= 0 && m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_lpDevHandle != IntPtr.Zero)
            {
                m_config.saveOSDInfo(stOSDInfo);
            }
        }

        /*OSD Info
         * OSD
         */
        private void OSDRefreshBtn_Click(object sender, EventArgs e)
        {
            if (m_CurSelectTreeNodeInfo.dwDeviceIndex >= 0 && m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_lpDevHandle != IntPtr.Zero)
            {
                m_config.refreshOSDInfo();
            }
        }

        /*OSD Info
         * OSD
         */
        public void showOSDInfo(NETDEV_VIDEO_OSD_CFG_S stOSDInfo)
        {
            /* Time */
            this.OSDTimeCheckBox.Checked = Convert.ToBoolean(stOSDInfo.stTimeOSD.bEnableFlag);
            this.OSDTimePointXText.Text = Convert.ToString(stOSDInfo.stTimeOSD.stAreaScope.dwLocateX);
            this.OSDTimePointYText.Text = Convert.ToString(stOSDInfo.stTimeOSD.stAreaScope.dwLocateY);
            this.OSDDateCobBox.SelectedIndex = (int)stOSDInfo.stTimeOSD.udwDateFormat;
            this.OSDTimeCobBox.SelectedIndex = (int)stOSDInfo.stTimeOSD.udwTimeFormat;

            /* NAME */
            OSDNameCheckBox.Checked = Convert.ToBoolean(stOSDInfo.stNameOSD.bEnableFlag);
            OSDNameText.Text = GetDefaultString(stOSDInfo.stNameOSD.OSDText);
            OSDNamePointXText.Text = Convert.ToString(stOSDInfo.stNameOSD.stAreaScope.dwLocateX);
            OSDNamePointYText.Text = Convert.ToString(stOSDInfo.stNameOSD.stAreaScope.dwLocateY);

            /* Text */
            OSDText1CheckBox.Checked = Convert.ToBoolean(stOSDInfo.astTextOverlay[0].bEnableFlag);
            OSDText1.Text = GetDefaultString(stOSDInfo.astTextOverlay[0].OSDText);
            OSDText1PointX.Text = Convert.ToString(stOSDInfo.astTextOverlay[0].stAreaScope.dwLocateX);
            OSDText1PointY.Text = Convert.ToString(stOSDInfo.astTextOverlay[0].stAreaScope.dwLocateY);

            OSDText2CheckBox.Checked = Convert.ToBoolean(stOSDInfo.astTextOverlay[1].bEnableFlag);
            OSDText2.Text = GetDefaultString(stOSDInfo.astTextOverlay[1].OSDText);
            OSDText2PointX.Text = Convert.ToString(stOSDInfo.astTextOverlay[1].stAreaScope.dwLocateX);
            OSDText2PointY.Text = Convert.ToString(stOSDInfo.astTextOverlay[1].stAreaScope.dwLocateY);

            OSDText3CheckBox.Checked = Convert.ToBoolean(stOSDInfo.astTextOverlay[2].bEnableFlag);
            OSDText3.Text = GetDefaultString(stOSDInfo.astTextOverlay[2].OSDText);
            OSDText3PointX.Text = Convert.ToString(stOSDInfo.astTextOverlay[2].stAreaScope.dwLocateX);
            OSDText3PointY.Text = Convert.ToString(stOSDInfo.astTextOverlay[2].stAreaScope.dwLocateY);

            OSDText4CheckBox.Checked = Convert.ToBoolean(stOSDInfo.astTextOverlay[3].bEnableFlag);
            OSDText4.Text = GetDefaultString(stOSDInfo.astTextOverlay[3].OSDText);
            OSDText4PointX.Text = Convert.ToString(stOSDInfo.astTextOverlay[3].stAreaScope.dwLocateX);
            OSDText4PointY.Text = Convert.ToString(stOSDInfo.astTextOverlay[3].stAreaScope.dwLocateY);

            OSDText5CheckBox.Checked = Convert.ToBoolean(stOSDInfo.astTextOverlay[4].bEnableFlag);
            OSDText5.Text = GetDefaultString(stOSDInfo.astTextOverlay[4].OSDText);
            OSDText5PointX.Text = Convert.ToString(stOSDInfo.astTextOverlay[4].stAreaScope.dwLocateX);
            OSDText5PointY.Text = Convert.ToString(stOSDInfo.astTextOverlay[4].stAreaScope.dwLocateY);

            OSDText6CheckBox.Checked = Convert.ToBoolean(stOSDInfo.astTextOverlay[5].bEnableFlag);
            OSDText6.Text = GetDefaultString(stOSDInfo.astTextOverlay[5].OSDText);
            OSDText6PointX.Text = Convert.ToString(stOSDInfo.astTextOverlay[5].stAreaScope.dwLocateX);
            OSDText6PointY.Text = Convert.ToString(stOSDInfo.astTextOverlay[5].stAreaScope.dwLocateY);
        }

        //Alarm out
        private void IOAlarmOutputTriggerBtn_Click(object sender, EventArgs e)
        {
            if (m_CurSelectTreeNodeInfo.dwDeviceIndex < 0 || m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_lpDevHandle == IntPtr.Zero)
            {
                return;
            }

            Int32 dwIndex = IOAlarmOutputIndexCobBox.SelectedIndex;
            if (dwIndex < 0 || dwIndex > NETDEVSDK.NETDEV_MAX_ALARM_OUT_NUM)
            {
                return;
            }

            NETDEV_TRIGGER_ALARM_OUTPUT_S stTriggerAlarmOutput = new NETDEV_TRIGGER_ALARM_OUTPUT_S();
            stTriggerAlarmOutput.szName = m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_channelInfoList[getChannelIndex()].m_IOInfo.stOutPutInfo.astAlarmOutputInfo[dwIndex].szName;

            stTriggerAlarmOutput.enOutputState = NETDEV_RELAYOUTPUT_STATE_E.NETDEV_BOOLEAN_STATUS_ACTIVE;

            int iRet = NETDEVSDK.NETDEV_SetDevConfig(m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_lpDevHandle, getChannelID(), (int)NETDEV_CONFIG_COMMAND_E.NETDEV_TRIGGER_ALARM_OUTPUT, ref stTriggerAlarmOutput, Marshal.SizeOf(stTriggerAlarmOutput));
            if (NETDEVSDK.TRUE != iRet)
            {
                showFailLogInfo(m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_ip + " chl:" + (getChannelID()), "Trigger alarm out", NETDEVSDK.NETDEV_GetLastError());
                return;
            }

            showSuccessLogInfo(m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_ip + " chl:" + (getChannelID()), "Trigger alarm out");
        }

        //Alarm out
        private void IOAlarmOutputSaveBtn_Click(object sender, EventArgs e)
        {
            if (m_CurSelectTreeNodeInfo.dwDeviceIndex < 0 || m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_lpDevHandle == IntPtr.Zero)
            {
                return;
            }

            Int32 dwIndex = IOAlarmOutputIndexCobBox.SelectedIndex;
            if (dwIndex < 0 || dwIndex > NETDEVSDK.NETDEV_MAX_ALARM_OUT_NUM)
            {
                return;
            }

            NETDEV_ALARM_OUTPUT_INFO_S stAlarmOutputInfo = new NETDEV_ALARM_OUTPUT_INFO_S();

            stAlarmOutputInfo.dwChancelId = Convert.ToInt32(this.IOAlarmOutputChannelID.Text);
            stAlarmOutputInfo.dwDurationSec = Convert.ToInt32(IOAlarmOutputDelayText.Text);
            if ((int)NETDEV_BOOLEAN_MODE_E.NETDEV_BOOLEAN_MODE_OPEN -1 == IOAlarmOutputStatusCobBox.SelectedIndex)
            {
                stAlarmOutputInfo.enDefaultStatus = (int)NETDEV_BOOLEAN_MODE_E.NETDEV_BOOLEAN_MODE_OPEN;
            }
            else
            {
                stAlarmOutputInfo.enDefaultStatus = (int)NETDEV_BOOLEAN_MODE_E.NETDEV_BOOLEAN_MODE_CLOSE;
            }
            
            stAlarmOutputInfo.szName = this.IOAlarmOutputNameText.Text;
            if (true == m_config.saveAlarmOutputInfo(stAlarmOutputInfo))
            {

                m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_channelInfoList[getChannelIndex()].m_IOInfo.stOutPutInfo.astAlarmOutputInfo[dwIndex] = stAlarmOutputInfo;
            }
        }

        //Refreash
        private void IORefreshBtn_Click(object sender, EventArgs e)
        {
            if (m_CurSelectTreeNodeInfo.dwDeviceIndex >= 0 && m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_lpDevHandle != IntPtr.Zero)
            {
                m_config.refreshIOInfo();
            }
        }

        //Alarm input
        public void showAlarmInputInfo(NETDEV_ALARM_INPUT_LIST_S stAlarmInputList)
        {
            IOAlarmInputListView.Items.Clear();
            for (Int32 i = 0; i < stAlarmInputList.dwSize; i++)
            {
                ListViewItem item = new ListViewItem(stAlarmInputList.astAlarmInputInfo[i].szName);
                this.IOAlarmInputListView.Items.Add(item);
            }
        }

        //Alarm output
        public void showAlarmOutputInfo(int index)
        {
            if (this.m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_channelInfoList[getChannelIndex()].m_IOInfo.stOutPutInfo.dwSize <= 0)
            {
                initIOAlarmOutputCfgTab();
                return;
            }

            IOAlarmOutputIndexCobBox.Items.Clear();
            for (Int32 i = 0; i < this.m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_channelInfoList[getChannelIndex()].m_IOInfo.stOutPutInfo.dwSize; i++)
            {
                IOAlarmOutputIndexCobBox.Items.Add(i);
            }

            IOAlarmOutputIndexCobBox.SelectedIndex = 0;
        }

        public void initIOCfgTab()
        {
            IOAlarmInputListView.Items.Clear();
            IOAlarmOutputIndexCobBox.Items.Clear();
            this.IOAlarmOutputNameText.Text = "";
            IOAlarmOutputStatusCobBox.SelectedIndex = 1;
            IOAlarmOutputChannelID.Text = "0";
            IOAlarmOutputDelayText.Text = "0";

        }

        public void initIOAlarmOutputCfgTab()
        {
            IOAlarmOutputIndexCobBox.Items.Clear();
            this.IOAlarmOutputNameText.Text = "";
            IOAlarmOutputStatusCobBox.SelectedIndex = 1;
            IOAlarmOutputChannelID.Text = "0";
            IOAlarmOutputDelayText.Text = "0";
        }

        private void IOAlarmOutputIndexCobBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            Int32 dwIndex = IOAlarmOutputIndexCobBox.SelectedIndex;

            showAlarmOutputInfoByIndex(dwIndex);
        }

        private void showAlarmOutputInfoByIndex(int index)
        {
            if (index < 0 || index > NETDEVSDK.NETDEV_MAX_ALARM_OUT_NUM)
            {
                return;
            }

            IOAlarmOutputDelayText.Text = Convert.ToString(m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_channelInfoList[getChannelIndex()].m_IOInfo.stOutPutInfo.astAlarmOutputInfo[index].dwDurationSec);
            IOAlarmOutputChannelID.Text = Convert.ToString(m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_channelInfoList[getChannelIndex()].m_IOInfo.stOutPutInfo.astAlarmOutputInfo[index].dwChancelId);

            if ((int)NETDEV_BOOLEAN_MODE_E.NETDEV_BOOLEAN_MODE_OPEN == m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_channelInfoList[getChannelIndex()].m_IOInfo.stOutPutInfo.astAlarmOutputInfo[index].enDefaultStatus)
            {
                IOAlarmOutputStatusCobBox.SelectedIndex = (int)NETDEV_BOOLEAN_MODE_E.NETDEV_BOOLEAN_MODE_OPEN - 1;
            }
            else
            {
                IOAlarmOutputStatusCobBox.SelectedIndex = (int)NETDEV_BOOLEAN_MODE_E.NETDEV_BOOLEAN_MODE_CLOSE - 1;
            }

            this.IOAlarmOutputNameText.Text = m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_channelInfoList[getChannelIndex()].m_IOInfo.stOutPutInfo.astAlarmOutputInfo[index].szName;
        }

        // Privacy Mask Info
        private void PrivacyMaskAddBtn_Click(object sender, EventArgs e)
        {
            if (m_CurSelectTreeNodeInfo.dwDeviceIndex < 0 || m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_lpDevHandle == IntPtr.Zero)
            {
                return;
            }

            if (privacyMaskInfoListView.Items.Count >= NETDEVSDK.NETDEV_MAX_PRIVACY_MASK_AREA_NUM)
            {
                MessageBox.Show("No more mask area is allowed","warning");
                return;
            }

            Int32 dwIndex = 0;
            Int32 dwInsertIndex = 0;
            for (Int32 i = 0; i < NETDEVSDK.NETDEV_MAX_PRIVACY_MASK_AREA_NUM; i++)
            {
                dwInsertIndex = i + 1;
                if (i < privacyMaskInfoListView.Items.Count)
                {
                    dwIndex = Convert.ToInt32(privacyMaskInfoListView.Items[i].SubItems[0].Text);

                    if (dwInsertIndex >= dwIndex)
                    {
                        continue;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            ListViewItem item = new ListViewItem(Convert.ToString(dwInsertIndex));
            item.SubItems.Add("0");
            item.SubItems.Add("0");
            item.SubItems.Add("1000");
            item.SubItems.Add("1000");

            this.privacyMaskInfoListView.Items.Add(item);

            PrivacyMaskSaveBtn_Click(null,null);
            PrivacyMaskRefreshBtn_Click(null,null);
        }

        // Del Privacy Mask Info
        private void PrivacyMaskDelBtn_Click(object sender, EventArgs e)
        {
            if (m_CurSelectTreeNodeInfo.dwDeviceIndex < 0 || m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_lpDevHandle == IntPtr.Zero)
            {
                return;
            }

            if (0 == privacyMaskInfoListView.SelectedIndices.Count)
            {
                return;
            }

            int count = privacyMaskInfoListView.SelectedIndices.Count;
            while(count > 0)
            {
                m_config.deletePrivacyMaskInfo(Convert.ToInt32(privacyMaskInfoListView.SelectedItems[count - 1].SubItems[0].Text));
                privacyMaskInfoListView.Items.Remove(privacyMaskInfoListView.Items[privacyMaskInfoListView.SelectedIndices[count - 1]]);
                count--;
            }

            PrivacyMaskSaveBtn_Click(null,null);
        }

        //Save Privacy Mask Info
        private void PrivacyMaskSaveBtn_Click(object sender, EventArgs e)
        {
            if (m_CurSelectTreeNodeInfo.dwDeviceIndex < 0 || m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_lpDevHandle == IntPtr.Zero)
            {
                return;
            }

            NETDEV_PRIVACY_MASK_CFG_S stPrivacyMaskInfo = new NETDEV_PRIVACY_MASK_CFG_S();
            stPrivacyMaskInfo.astArea = new NETDEV_PRIVACY_MASK_AREA_INFO_S[NETDEVSDK.NETDEV_MAX_PRIVACY_MASK_AREA_NUM];

            getListViewData(ref stPrivacyMaskInfo);
            m_config.savePrivacyMaskInfo(stPrivacyMaskInfo);
            
        }

        private void getListViewData(ref NETDEV_PRIVACY_MASK_CFG_S stPrivacyMaskInfo)
        {
            for (Int32 i = 0; i < this.privacyMaskInfoListView.Items.Count; i++)
            {
                stPrivacyMaskInfo.astArea[i].dwIndex = Convert.ToInt32(this.privacyMaskInfoListView.Items[i].SubItems[0].Text);
                stPrivacyMaskInfo.astArea[i].dwTopLeftX = Convert.ToInt32(this.privacyMaskInfoListView.Items[i].SubItems[1].Text);
                stPrivacyMaskInfo.astArea[i].dwTopLeftY = Convert.ToInt32(this.privacyMaskInfoListView.Items[i].SubItems[2].Text);
                stPrivacyMaskInfo.astArea[i].dwBottomRightX = Convert.ToInt32(this.privacyMaskInfoListView.Items[i].SubItems[3].Text);
                stPrivacyMaskInfo.astArea[i].dwBottomRightY = Convert.ToInt32(this.privacyMaskInfoListView.Items[i].SubItems[4].Text);
                stPrivacyMaskInfo.astArea[i].bIsEanbled = NETDEVSDK.TRUE;
            }

            stPrivacyMaskInfo.dwSize = this.privacyMaskInfoListView.Items.Count;
        }

        //refreash Privacy Mask Info
        private void PrivacyMaskRefreshBtn_Click(object sender, EventArgs e)
        {
            if (m_CurSelectTreeNodeInfo.dwDeviceIndex >= 0 && m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_lpDevHandle != IntPtr.Zero)
            {
                m_config.refreshPrivacyMaskInfo();
            }
        }

        //show Privacy Mask Info
        public void showPrivacyMaskInfo(NETDEV_PRIVACY_MASK_CFG_S stPrivacyMaskInfo)
        {
            privacyMaskInfoListView.Items.Clear();
            for (Int32 i = 0; i < stPrivacyMaskInfo.dwSize; i++)
            {
                ListViewItem item = new ListViewItem(Convert.ToString(stPrivacyMaskInfo.astArea[i].dwIndex));
                item.SubItems.Add(Convert.ToString(stPrivacyMaskInfo.astArea[i].dwTopLeftX));
                item.SubItems.Add(Convert.ToString(stPrivacyMaskInfo.astArea[i].dwTopLeftY));
                item.SubItems.Add(Convert.ToString(stPrivacyMaskInfo.astArea[i].dwBottomRightX));
                item.SubItems.Add(Convert.ToString(stPrivacyMaskInfo.astArea[i].dwBottomRightY));
                item.SubItems[0].Name = "index";
                item.SubItems[1].Name = "TopLeftX";
                item.SubItems[2].Name = "TopLeftY";
                item.SubItems[3].Name = "BottomRightX";
                item.SubItems[4].Name = "BottomRightY";
                this.privacyMaskInfoListView.Items.Add(item);
            }
        }

        private void privacyMaskInfoListView_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            Point tmpPoint = privacyMaskInfoListView.PointToClient(Cursor.Position);
            ListViewItem.ListViewSubItem subitem = privacyMaskInfoListView.HitTest(tmpPoint).SubItem;

            if (subitem != null && subitem.Name != "index")
            {
                this.privacyMaskSubItemText.Text = subitem.Text;
                this.strSubItemName = subitem.Name;
                this.iItemIndex = privacyMaskInfoListView.SelectedIndices[0];
                this.privacyMaskSubItemText.Enabled = true;
                this.privacyMaskModifyBtn.Enabled = true;
            }
            else
            {
                this.privacyMaskSubItemText.Enabled = false;
                this.privacyMaskModifyBtn.Enabled = false;
            }

        }

        // Modify Privacy Mask Info
        private void privacyMaskModifyBtn_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < privacyMaskInfoListView.Items[this.iItemIndex].SubItems.Count; i++)
            {
                if (this.strSubItemName == privacyMaskInfoListView.Items[this.iItemIndex].SubItems[i].Name)
                {
                    privacyMaskInfoListView.Items[this.iItemIndex].SubItems[i].Text = privacyMaskSubItemText.Text;
                    this.privacyMaskSubItemText.Enabled = false;
                    this.privacyMaskModifyBtn.Enabled = false;
                }
            }
        }

        private void MotionSensitivityTrackBar_Scroll(object sender, EventArgs e)
        {
            if (MotionSensitivityTrackBar.Value > 100 || MotionSensitivityTrackBar.Value < 1)
            {
                MotionSensitivityTrackBar.Value = 50;
            }
            MotionSensitivityText.Text = Convert.ToString(MotionSensitivityTrackBar.Value);
        }

        private void MotionObjectSizeTrackBar_Scroll(object sender, EventArgs e)
        {
            if (MotionObjectSizeTrackBar.Value > 100 || MotionObjectSizeTrackBar.Value < 1)
            {
                MotionObjectSizeTrackBar.Value = 50;
            }
            MotionObjectSizeText.Text = Convert.ToString(MotionObjectSizeTrackBar.Value);
        }

        private void MotionHistoryTrackBar_Scroll(object sender, EventArgs e)
        {
            if (MotionHistoryTrackBar.Value > 100 || MotionHistoryTrackBar.Value < 1)
            {
                MotionHistoryTrackBar.Value = 50;
            }
            MotionHistoryText.Text = Convert.ToString(MotionHistoryTrackBar.Value);
        }

        private void MotionSensitivityText_TextChanged(object sender, EventArgs e)
        {
            try
            {
                if (Convert.ToInt32(MotionSensitivityText.Text) > 100 || Convert.ToInt32(MotionSensitivityText.Text) < 1)
                {
                    MotionSensitivityText.Text = "50";
                }
            }
            catch (FormatException)
            {
                return;
            }

            MotionSensitivityTrackBar.Value = Convert.ToInt32(MotionSensitivityText.Text);
        }

        private void MotionObjectSizeText_TextChanged(object sender, EventArgs e)
        {
            try
            {
                if (Convert.ToInt32(MotionObjectSizeText.Text) > 100 || Convert.ToInt32(MotionObjectSizeText.Text) < 1)
                {
                    MotionObjectSizeText.Text = "50";
                }
            }
            catch (FormatException)
            {
                return;
            }

            MotionObjectSizeTrackBar.Value = Convert.ToInt32(MotionObjectSizeText.Text);
        }

        private void MotionHistoryText_TextChanged(object sender, EventArgs e)
        {
            try
            {
                if (Convert.ToInt32(MotionHistoryText.Text) > 100 || Convert.ToInt32(MotionHistoryText.Text) < 1)
                {
                    MotionHistoryText.Text = "50";
                }
            }
            catch (FormatException)
            {
                return;
            }
            MotionHistoryTrackBar.Value = Convert.ToInt32(MotionHistoryText.Text);
        }

        private void MotionSaveBtn_Click(object sender, EventArgs e)
        {
            if (m_CurSelectTreeNodeInfo.dwDeviceIndex < 0 || m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_lpDevHandle == IntPtr.Zero)
            {
                return;
            }

            NETDEV_MOTION_ALARM_INFO_S stMotionAlarmInfo = new NETDEV_MOTION_ALARM_INFO_S();
            stMotionAlarmInfo.awScreenInfo = new NETDEV_SCREENINFO_COLUMN_S[NETDEVSDK.NETDEV_SCREEN_INFO_ROW];

            for (int i = 0; i < NETDEVSDK.NETDEV_SCREEN_INFO_ROW; i++)
            {
                stMotionAlarmInfo.awScreenInfo[i].columnInfo = new short[NETDEVSDK.NETDEV_SCREEN_INFO_COLUMN];
                for (int j = 0; j < NETDEVSDK.NETDEV_SCREEN_INFO_COLUMN; j++)
                {
                    stMotionAlarmInfo.awScreenInfo[i].columnInfo[j] = 1;
                }
            }

            
            stMotionAlarmInfo.dwSensitivity = MotionSensitivityTrackBar.Value;
            stMotionAlarmInfo.dwObjectSize = MotionObjectSizeTrackBar.Value;
            stMotionAlarmInfo.dwHistory = MotionHistoryTrackBar.Value;
            m_config.saveMotionInfo(ref stMotionAlarmInfo);
        }

        private void MotionRefreshBtn_Click(object sender, EventArgs e)
        {
            if (m_CurSelectTreeNodeInfo.dwDeviceIndex >= 0 && m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_lpDevHandle != IntPtr.Zero)
            {
                m_config.refreshMotionInfo();
            }
        }

        public void showMotionInfo(ref NETDEV_MOTION_ALARM_INFO_S stMotionAlarmInfo)
        {
            if (stMotionAlarmInfo.dwSensitivity > 100 || stMotionAlarmInfo.dwSensitivity < 1 ||
                stMotionAlarmInfo.dwObjectSize > 100 || stMotionAlarmInfo.dwObjectSize < 1 ||
                stMotionAlarmInfo.dwHistory > 100 || stMotionAlarmInfo.dwHistory < 1)
            {
                showFailLogInfo(m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_ip + " chl:" + (getChannelID()), "Get motion cfg", NETDEVSDK.NETDEV_GetLastError());
                return;
            }
            else
            {
                MotionSensitivityTrackBar.Value = stMotionAlarmInfo.dwSensitivity;
                MotionObjectSizeTrackBar.Value = stMotionAlarmInfo.dwObjectSize;
                MotionHistoryTrackBar.Value = stMotionAlarmInfo.dwHistory;

                MotionSensitivityText.Text = Convert.ToString(stMotionAlarmInfo.dwSensitivity);
                MotionObjectSizeText.Text = Convert.ToString(stMotionAlarmInfo.dwObjectSize);
                MotionHistoryText.Text = Convert.ToString(stMotionAlarmInfo.dwHistory);
            }
        }

        private void TemperSensitivityTrackBar_Scroll(object sender, EventArgs e)
        {
            if (TemperSensitivityTrackBar.Value > 100 || TemperSensitivityTrackBar.Value < 0)
            {
                TemperSensitivityTrackBar.Value = 50;
            }
            TemperSensitivityText.Text = Convert.ToString(TemperSensitivityTrackBar.Value);
        }

        private void TemperSensitivityText_TextChanged(object sender, EventArgs e)
        {
            try
            {
                if (Convert.ToInt32(TemperSensitivityText.Text) > 100 || Convert.ToInt32(TemperSensitivityText.Text) < 0)
                {
                    TemperSensitivityText.Text = "50";
                }
            }
            catch (FormatException)
            {
                return;
            }

            TemperSensitivityTrackBar.Value = Convert.ToInt32(TemperSensitivityText.Text);
        }

        public void showTemperInfo(ref NETDEV_TAMPER_ALARM_INFO_S stTamperAlarmInfo)
        {
            TemperSensitivityText.Text = Convert.ToString(stTamperAlarmInfo.dwSensitivity);
            TemperSensitivityTrackBar.Value = stTamperAlarmInfo.dwSensitivity;
        }

        private void TemperSaveBtn_Click(object sender, EventArgs e)
        {
            if (m_CurSelectTreeNodeInfo.dwDeviceIndex < 0 || m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_lpDevHandle == IntPtr.Zero)
            {
                return;
            }

            NETDEV_TAMPER_ALARM_INFO_S stTamperAlarmInfo = new NETDEV_TAMPER_ALARM_INFO_S();

            stTamperAlarmInfo.dwSensitivity = TemperSensitivityTrackBar.Value;
            m_config.saveTemperInfo(stTamperAlarmInfo);
        }

         private void TemperRefreshBtn_Click(object sender, EventArgs e)
        {
            if (m_CurSelectTreeNodeInfo.dwDeviceIndex >= 0 && m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_lpDevHandle != IntPtr.Zero)
            {
                m_config.refreshTemperInfo();
            }
        }
        
        public void alarmMessCallBack(IntPtr lpUserID, Int32 dwChannelID, NETDEV_ALARM_INFO_S stAlarmInfo, IntPtr lpBuf, Int32 dwBufLen, IntPtr lpUserData)
        {
            String strAlarmTime = getStrTime(stAlarmInfo.tAlarmTime);
            ListViewItem item = new ListViewItem(strAlarmTime);

            String strDeviceIP = getDeviceIP(lpUserID);
            item.SubItems.Add(strDeviceIP);

            item.SubItems.Add(Convert.ToString(dwChannelID));

            String strAlarmInfo = getAlarmInfo(stAlarmInfo.dwAlarmType);
            
            item.SubItems.Add(strAlarmInfo);
            AlarmRecordsListView.Items.Add(item);
        }

        public void alarmMessCallBackV30(IntPtr lpUserID, ref NETDEV_REPORT_INFO_S pstReportInfo, IntPtr lpBuf, Int32 dwBufLen, IntPtr lpUserData)
        {
            if ((int)NETDEV_REPORT_TYPE_E.NETDEV_REPORT_TYPE_EVENT == pstReportInfo.dwReportType)
            {
                for (int i = 0; i < pstReportInfo.stEventInfo.dwSize; i++)
                {
                    switch (pstReportInfo.stEventInfo.astEventRes[i].dwResType)
                    {
                        case (int)NETDEV_EVENT_RES_TYPE_E.NETDEV_EVENT_RES_TYPE_DEVICE:
                            {
                                switch (pstReportInfo.stEventInfo.dwEventActionType)
                                {
                                    case (int)NETDEV_EVENT_ACTION_TYPE_E.NETDEV_EVENT_ACTION_TYPE_ADD:
                                    case (int)NETDEV_EVENT_ACTION_TYPE_E.NETDEV_EVENT_ACTION_TYPE_MODIFY:
                                    case (int)NETDEV_EVENT_ACTION_TYPE_E.NETDEV_EVENT_ACTION_TYPE_OFFLINE:
                                    case (int)NETDEV_EVENT_ACTION_TYPE_E.NETDEV_EVENT_ACTION_TYPE_ONLINE:
                                        {
                                            updateSubDevice(lpUserID, pstReportInfo.stEventInfo.astEventRes[i].dwResID);
                                            break;
                                        }
                                    case (int)NETDEV_EVENT_ACTION_TYPE_E.NETDEV_EVENT_ACTION_TYPE_DELETE:
                                        {
                                            deleteSubDevice(lpUserID, pstReportInfo.stEventInfo.astEventRes[i].dwResID);
                                            break;
                                        }
                                    default:
                                            break;
                                }
                                break;
                            }
                        case (int)NETDEV_EVENT_RES_TYPE_E.NETDEV_EVENT_RES_TYPE_CHANNEL:
                            {
                                switch (pstReportInfo.stEventInfo.dwEventActionType)
                                {
                                    case (int)NETDEV_EVENT_ACTION_TYPE_E.NETDEV_EVENT_ACTION_TYPE_ADD:
                                    case (int)NETDEV_EVENT_ACTION_TYPE_E.NETDEV_EVENT_ACTION_TYPE_MODIFY:
                                    case (int)NETDEV_EVENT_ACTION_TYPE_E.NETDEV_EVENT_ACTION_TYPE_OFFLINE:
                                    case (int)NETDEV_EVENT_ACTION_TYPE_E.NETDEV_EVENT_ACTION_TYPE_ONLINE:
                                        {
                                            updateDeviceChannel(lpUserID, pstReportInfo.stEventInfo.astEventRes[i].dwResID);
                                            break;
                                        }
                                    case (int)NETDEV_EVENT_ACTION_TYPE_E.NETDEV_EVENT_ACTION_TYPE_DELETE:
                                        {
                                            deleteDeviceChannel(lpUserID, pstReportInfo.stEventInfo.astEventRes[i].dwResID);
                                            break;
                                        }
                                    default:
                                            break;
                                }
                                break;
                            }

                        default:
                            break;
                    }
                }
            }
            else if ((int)NETDEV_REPORT_TYPE_E.NETDEV_REPORT_TYPE_ALARM == pstReportInfo.dwReportType)
            {
                String strAlarmTime = getStrTime(pstReportInfo.stAlarmInfo.tAlarmTimeStamp);
                ListViewItem item = new ListViewItem(strAlarmTime);

                String strDeviceIP = getDeviceIP(lpUserID);
                item.SubItems.Add(strDeviceIP);

                item.SubItems.Add(Convert.ToString(pstReportInfo.stAlarmInfo.dwChannelID));

                String strAlarmInfo = getAlarmInfo(pstReportInfo.stAlarmInfo.dwAlarmType);

                item.SubItems.Add(strAlarmInfo);
                AlarmRecordsListView.Items.Add(item);
            }
        }
        
        public void exceptionCallBack(IntPtr lpUserID, Int32 dwType, IntPtr lpExpHandle, IntPtr lpUserData)
        {
            String strAlarmTime = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
            ListViewItem item = new ListViewItem(strAlarmTime);

            String strDeviceIP = getDeviceIP(lpUserID);
            item.SubItems.Add(strDeviceIP);

            item.SubItems.Add("");
            String strAlarmInfo = getAlarmInfo(dwType);
            item.SubItems.Add(strAlarmInfo);
            AlarmRecordsListView.Items.Add(item);

            if ((int)NETDEV_EXCEPTION_TYPE_E.NETDEV_EXCEPTION_EXCHANGE == dwType)
            {
                for (int dwIndex = 0; dwIndex < m_deviceInfoList.Count; dwIndex++)
                {
                    if (NETDEMO.NETDEMO_DEVICE_TYPE_E.NETDEMO_DEVICE_INVALID == m_deviceInfoList[dwIndex].m_eDeviceType)
                    {
                        continue;
                    }

                    if (m_deviceInfoList[dwIndex].m_lpDevHandle == lpUserID)
                    {
                        closeSelectedDeviceRealPlay(m_deviceInfoList[dwIndex], dwIndex, false);
                    }
                }
            }
        }

        private void FaceSnapshotCallBack(IntPtr lpUserID, ref NETDEV_TMS_FACE_SNAPSHOT_PIC_INFO_S pstFaceSnapShotData, IntPtr lpUserParam)
        {
            String strNowTime = DateTime.Now.ToString("HH-mm-ss-ms");
            String strFileName = strNowTime + "_" + pstFaceSnapShotData.udwFaceId.ToString() + "face.jpg";

            byte[] array = new byte[pstFaceSnapShotData.udwPicBuffLen];
            NETDEVSDK.MemCopy(array, pstFaceSnapShotData.pcPicBuff, pstFaceSnapShotData.udwPicBuffLen);

            FileStream fs = new FileStream(strFileName, FileMode.Create);
            //将byte数组写入文件中
            fs.Write(array, 0, array.Length);
            //所有流类型都要关闭流，否则会出现内存泄露问题
            fs.Close();
        }

        private void updateDeviceChannel(IntPtr lpUserID, int dwChannelID)
        {
            int pdwChnType = 0;
            Int32 iRet = NETDEVSDK.NETDEV_GetChnType(lpUserID, dwChannelID, ref pdwChnType);
            if(NETDEVSDK.FALSE == iRet)
            {
                return;
            }

            if((int)NETDEV_CHN_TYPE_E.NETDEV_CHN_TYPE_ENCODE == pdwChnType)
            {
                int pdwBytesReturned = 0;
                NETDEV_DEV_CHN_ENCODE_INFO_S stDevChnInfo = new NETDEV_DEV_CHN_ENCODE_INFO_S();
                IntPtr lpOutBuffer = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(NETDEV_DEV_CHN_ENCODE_INFO_S)));
                Marshal.StructureToPtr(stDevChnInfo, lpOutBuffer, true);
                iRet = NETDEVSDK.NETDEV_GetChnDetailByChnType(lpUserID,dwChannelID,pdwChnType,lpOutBuffer, Marshal.SizeOf(typeof(NETDEV_DEV_CHN_ENCODE_INFO_S)), ref pdwBytesReturned);
                if (NETDEVSDK.FALSE == iRet)
                {
                    Marshal.FreeHGlobal(lpOutBuffer);
                    return;
                }
                else
                {
                    stDevChnInfo = (NETDEV_DEV_CHN_ENCODE_INFO_S)Marshal.PtrToStructure(lpOutBuffer, typeof(NETDEV_DEV_CHN_ENCODE_INFO_S));
                    Marshal.FreeHGlobal(lpOutBuffer);
                }

                int dwDeviceIndex = -1;
                int dwOrgIndex = -1;
                int dwSubDeviceIndex = -1;
                int dwChannelIndex = -1;

                for (int i = 0; i < m_deviceInfoList.Count; i++)
                {
                    if (NETDEMO.NETDEMO_DEVICE_TYPE_E.NETDEMO_DEVICE_INVALID == m_deviceInfoList[i].m_eDeviceType)
                    {
                        continue;
                    }

                    if (lpUserID == m_deviceInfoList[i].m_lpDevHandle)
                    {
                        dwDeviceIndex = i;
                        break;
                    }
                }

                dwOrgIndex = getOrgIndexByID(dwDeviceIndex, stDevChnInfo.stChnBaseInfo.dwOrgID);
                dwSubDeviceIndex = getSubDeviceIndexByID(dwDeviceIndex, dwOrgIndex, stDevChnInfo.stChnBaseInfo.dwDevID);
                dwChannelIndex = getChannelIndexByID(dwDeviceIndex, dwOrgIndex, dwSubDeviceIndex, dwChannelID);

                if (-1 == dwSubDeviceIndex)
                {
                    return;
                }

                NETDEMO_VMS_DEV_CHANNEL_INFO_S stDemoVmsChnInfo = new NETDEMO_VMS_DEV_CHANNEL_INFO_S();
                stDemoVmsChnInfo.stChnInfo = stDevChnInfo;

                /* 已经存在 */
                TreeNode treeNode = TreeViewFindNode(DeviceTree, dwDeviceIndex, stDevChnInfo.stChnBaseInfo.dwChannelID, NETDEMO.NETDEMO_FIND_TREE_NODE_TYPE_E.NETDEMO_FIND_TREE_NODE_CHN_ID);
                if (null != treeNode)
                {
                    m_deviceInfoList[dwDeviceIndex].stVmsDevInfo.stOrgInfoList[dwOrgIndex].stVmsDevBasicInfoList[dwSubDeviceIndex].stChnInfoList[dwChannelIndex] = stDemoVmsChnInfo;
                    updateChnTreeNode(treeNode, stDevChnInfo);
                    return;
                }

                m_deviceInfoList[dwDeviceIndex].stVmsDevInfo.stOrgInfoList[dwOrgIndex].stVmsDevBasicInfoList[dwSubDeviceIndex].stChnInfoList.Add(stDemoVmsChnInfo);

                treeNode = TreeViewFindNode(DeviceTree, dwDeviceIndex, stDevChnInfo.stChnBaseInfo.dwDevID, NETDEMO.NETDEMO_FIND_TREE_NODE_TYPE_E.NETDEMO_FIND_TREE_NODE_SUB_DEVICE_ID);
                if (null != treeNode)
                {
                    updateChnTreeNode(treeNode, stDevChnInfo);
                }
            }
        }

        private void deleteDeviceChannel(IntPtr lpUserID, int dwChannelID)
        {
            int dwDeviceIndex = -1;
            int dwOrgIndex = -1;
            int dwSubDeviceIndex = -1;
            int dwChannelIndex = -1;

            for (int i = 0; i < m_deviceInfoList.Count; i++)
            {
                if (NETDEMO.NETDEMO_DEVICE_TYPE_E.NETDEMO_DEVICE_INVALID == m_deviceInfoList[i].m_eDeviceType)
                {
                    continue;
                }

                if (lpUserID == m_deviceInfoList[i].m_lpDevHandle)
                {
                    dwDeviceIndex = i;
                    break;
                }
            }

            TreeNode treeNode = TreeViewFindNode(DeviceTree, dwDeviceIndex, dwChannelID, NETDEMO.NETDEMO_FIND_TREE_NODE_TYPE_E.NETDEMO_FIND_TREE_NODE_CHN_ID);
            if (null != treeNode)
            {
                TreeNodeInfo treeNodeInfo = (TreeNodeInfo)treeNode.Tag;
                dwOrgIndex = getOrgIndexByID(dwDeviceIndex, treeNodeInfo.dwOrgID);
                dwSubDeviceIndex = getSubDeviceIndexByID(dwDeviceIndex, dwOrgIndex, treeNodeInfo.dwSubDeviceID);
                dwChannelIndex = getChannelIndexByID(dwDeviceIndex, dwOrgIndex, dwSubDeviceIndex, dwChannelID);

                m_deviceInfoList[dwDeviceIndex].stVmsDevInfo.stOrgInfoList[dwOrgIndex].stVmsDevBasicInfoList[dwSubDeviceIndex].stChnInfoList.Remove(m_deviceInfoList[dwDeviceIndex].stVmsDevInfo.stOrgInfoList[dwOrgIndex].stVmsDevBasicInfoList[dwSubDeviceIndex].stChnInfoList[dwChannelIndex]);
                treeNode.Nodes.Clear();
                treeNode.Remove();
            }
        }

        private void deleteSubDevice(IntPtr lpUserID, int dwDeviceID)
        {
            int dwDeviceIndex = -1;
            int dwOrgIndex = -1;
            int dwSubDeviceIndex = -1;

            for (int i = 0; i < m_deviceInfoList.Count; i++)
            {
                if (NETDEMO.NETDEMO_DEVICE_TYPE_E.NETDEMO_DEVICE_INVALID == m_deviceInfoList[i].m_eDeviceType)
                {
                    continue;
                }

                if (lpUserID == m_deviceInfoList[i].m_lpDevHandle)
                {
                    dwDeviceIndex = i;
                    break;
                }
            }

            TreeNode treeNode = TreeViewFindNode(DeviceTree, dwDeviceIndex, dwDeviceID, NETDEMO.NETDEMO_FIND_TREE_NODE_TYPE_E.NETDEMO_FIND_TREE_NODE_SUB_DEVICE_ID);
            if (null != treeNode)
            {
                TreeNodeInfo treeNodeInfo = (TreeNodeInfo)treeNode.Tag;
                dwOrgIndex = getOrgIndexByID(dwDeviceIndex, treeNodeInfo.dwOrgID);
                dwSubDeviceIndex = getSubDeviceIndexByID(dwDeviceIndex, dwOrgIndex, dwDeviceID);

                m_deviceInfoList[treeNodeInfo.dwDeviceIndex].stVmsDevInfo.stOrgInfoList[dwOrgIndex].stVmsDevBasicInfoList.Remove(m_deviceInfoList[dwDeviceIndex].stVmsDevInfo.stOrgInfoList[dwOrgIndex].stVmsDevBasicInfoList[dwSubDeviceIndex]);
                treeNode.Nodes.Clear();
                treeNode.Remove();
            }
        }

        private void updateSubDevice(IntPtr lpUserID, int dwDeviceID)
        {
            NETDEMO_VMS_DEV_BASIC_INFO_S stDemoVmsBasicInfo = new NETDEMO_VMS_DEV_BASIC_INFO_S();
            NETDEV_DEV_INFO_V30_S stDevInfo = new NETDEV_DEV_INFO_V30_S();
            Int32 iRet = NETDEVSDK.NETDEV_GetDeviceInfo_V30(lpUserID, dwDeviceID, ref stDevInfo);
            if (NETDEVSDK.FALSE == iRet)
            {
                return;
            }

            if ((int)NETDEV_DEVICE_MAIN_TYPE_E.NETDEV_DTYPE_MAIN_ENCODE != stDevInfo.stDevBasicInfo.dwDevType)
            {
                return;
            }

            stDemoVmsBasicInfo.stDevBasicInfo = stDevInfo.stDevBasicInfo;

            IntPtr lpFindDevChnHandle = NETDEVSDK.NETDEV_FindDevChnList(lpUserID, stDevInfo.stDevBasicInfo.dwDevID, 0);
            if (IntPtr.Zero == lpFindDevChnHandle)
            {
                return;
            }

            while (true)
            {
                NETDEMO_VMS_DEV_CHANNEL_INFO_S stDemoVmsChnInfo = new NETDEMO_VMS_DEV_CHANNEL_INFO_S();

                int pdwBytesReturned = 0;
                NETDEV_DEV_CHN_ENCODE_INFO_S stDevChnInfo = new NETDEV_DEV_CHN_ENCODE_INFO_S();
                IntPtr lpOutBuffer = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(NETDEV_DEV_CHN_ENCODE_INFO_S)));
                Marshal.StructureToPtr(stDevChnInfo, lpOutBuffer, true);
                iRet = NETDEVSDK.NETDEV_FindNextDevChn(lpFindDevChnHandle, lpOutBuffer, Marshal.SizeOf(typeof(NETDEV_DEV_CHN_ENCODE_INFO_S)), ref pdwBytesReturned);
                if (NETDEVSDK.FALSE == iRet)
                {
                    Marshal.FreeHGlobal(lpOutBuffer);
                    break;
                }
                else
                {
                    stDevChnInfo = (NETDEV_DEV_CHN_ENCODE_INFO_S)Marshal.PtrToStructure(lpOutBuffer, typeof(NETDEV_DEV_CHN_ENCODE_INFO_S));
                    stDemoVmsChnInfo.stChnInfo = stDevChnInfo;
                    stDemoVmsBasicInfo.stChnInfoList.Add(stDemoVmsChnInfo);

                    Marshal.FreeHGlobal(lpOutBuffer);
                }
            }

            NETDEVSDK.NETDEV_FindCloseDevChn(lpFindDevChnHandle);

            int dwDeviceIndex = -1;
            int dwOrgIndex = -1;
            int dwSubDeviceIndex = -1;

            for (int i = 0; i < m_deviceInfoList.Count; i++)
            {
                if (NETDEMO.NETDEMO_DEVICE_TYPE_E.NETDEMO_DEVICE_INVALID == m_deviceInfoList[i].m_eDeviceType)
                {
                    continue;
                }

                if (lpUserID == m_deviceInfoList[i].m_lpDevHandle)
                {
                    dwDeviceIndex = i;
                    break;
                }
            }

            if (-1 == dwDeviceIndex)
            {
                return;
            }

            dwOrgIndex = getOrgIndexByID(dwDeviceIndex, stDevInfo.stDevBasicInfo.dwOrgID);
            if (-1 == dwOrgIndex)
            {
                return;
            }

            dwSubDeviceIndex = getSubDeviceIndexByID(dwDeviceIndex, dwOrgIndex, stDevInfo.stDevBasicInfo.dwDevID);
            if (-1 == dwSubDeviceIndex)
            {
                return;
            }

            TreeNode treeNode = TreeViewFindNode(DeviceTree, dwDeviceIndex, stDevInfo.stDevBasicInfo.dwDevID, NETDEMO.NETDEMO_FIND_TREE_NODE_TYPE_E.NETDEMO_FIND_TREE_NODE_SUB_DEVICE_ID);

            /* 已经存在 */
            if (null != treeNode)
            {
                m_deviceInfoList[dwDeviceIndex].stVmsDevInfo.stOrgInfoList[dwOrgIndex].stVmsDevBasicInfoList[dwSubDeviceIndex] = stDemoVmsBasicInfo;
                updateSubDeviceTreeNode(treeNode, stDemoVmsBasicInfo);
                return;
            }

            m_deviceInfoList[dwDeviceIndex].stVmsDevInfo.stOrgInfoList[dwOrgIndex].stVmsDevBasicInfoList.Add(stDemoVmsBasicInfo);
            
            treeNode = TreeViewFindNode(DeviceTree, dwDeviceIndex, stDevInfo.stDevBasicInfo.dwOrgID, NETDEMO.NETDEMO_FIND_TREE_NODE_TYPE_E.NETDEMO_FIND_TREE_NODE_ORG_ID);
            if (null != treeNode)
            {
                updateSubDeviceTreeNode(treeNode, stDemoVmsBasicInfo);
            }
        }

        private string getDeviceIP(IntPtr lpUserID)
        {
            String strDeviceIP = null;
            for (int i = 0; i < arrayRealPanel.Length; i++)
            {
                if (lpUserID == arrayRealPanel[i].m_playhandle && arrayRealPanel[i].m_playStatus == true)
                {
                    strDeviceIP = m_deviceInfoList[arrayRealPanel[i].m_deviceIndex].m_ip;
                    if (strDeviceIP == null)
                    {
                        strDeviceIP = m_deviceInfoList[arrayRealPanel[i].m_deviceIndex].m_cloudUrl;
                    }
                    break;
                }
            }

            for (int i = 0; i < m_deviceInfoList.Count; i++)
            {
                if (NETDEMO.NETDEMO_DEVICE_TYPE_E.NETDEMO_DEVICE_INVALID == m_deviceInfoList[i].m_eDeviceType)
                {
                    continue;
                }

                if (m_deviceInfoList[i].m_lpDevHandle == lpUserID)
                {
                    strDeviceIP = m_deviceInfoList[i].m_ip;
                    break;
                }
            }

            if (strDeviceIP == null)
            {
                strDeviceIP = "Unknown ip";
            }

            return strDeviceIP;
        }

        private void AlarmRecordsClearBtn_Click(object sender, EventArgs e)
        {
            AlarmRecordsListView.Items.Clear();
        }

        private String getAlarmInfo(int dwAlarmType)
        {
            String strAlarmInfo = null;

            for (int i = 0; i < NETDEMO.gastNETDemoAlarmInfo.Length; i++)
            {
                if (dwAlarmType == NETDEMO.gastNETDemoAlarmInfo[i].alarmType)
                {
                    strAlarmInfo = NETDEMO.gastNETDemoAlarmInfo[i].reportAlarm;
                    break;
                }
            }
            if (NETDEVSDK.NETDEV_E_VIDEO_RESOLUTION_CHANGE == dwAlarmType)
            {
                strAlarmInfo = "Resolution changed";
            }
            if (strAlarmInfo == null)
            {
                strAlarmInfo = "Unknown alarm";
            }

            return strAlarmInfo;
        }

        private void RebootBtn_Click(object sender, EventArgs e)
        {
            if (m_deviceInfoList.Count == 0)
            {
                return;
            }

            if ((DeviceTree.SelectedNode.ImageIndex == NETDEMO.NETDEV_TREEVIEW_IMAGE_LOCAL_DEVICE_ON
                || DeviceTree.SelectedNode.ImageIndex == NETDEMO.NETDEV_TREEVIEW_IMAGE_CLOUD_DEVICE_ON)
            && m_deviceInfoList[DeviceTree.SelectedNode.Index].m_lpDevHandle != IntPtr.Zero)
            {
                DialogResult result = MessageBox.Show("Do you want to restart the device?", "Info", MessageBoxButtons.OKCancel, MessageBoxIcon.Information);
                if (result == DialogResult.OK)
                {
                    int iRet = NETDEVSDK.NETDEV_Reboot(m_deviceInfoList[DeviceTree.SelectedNode.Index].m_lpDevHandle);
                    if (NETDEVSDK.TRUE != iRet)
                    {
                        showFailLogInfo(m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_ip + " chl:" + (getChannelID()), "Reboot", NETDEVSDK.NETDEV_GetLastError());
                    }
                    else
                    {
                        showSuccessLogInfo(m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_ip + " chl:" + (getChannelID()), "Reboot");
                        Logout_Click(null,null);
                    }
                }
                else
                {
                    return;
                }
            }
            else
            {
                MessageBox.Show("Cannot reboot channel","warning");
            }
        }

        private void factoryDefaultBtn_Click(object sender, EventArgs e)
        {
            if (m_deviceInfoList.Count == 0)
            {
                return;
            }

            if ((DeviceTree.SelectedNode.ImageIndex == NETDEMO.NETDEV_TREEVIEW_IMAGE_LOCAL_DEVICE_ON
                || DeviceTree.SelectedNode.ImageIndex == NETDEMO.NETDEV_TREEVIEW_IMAGE_CLOUD_DEVICE_ON) 
                && m_deviceInfoList[DeviceTree.SelectedNode.Index].m_lpDevHandle != IntPtr.Zero)
            {
                DialogResult result = MessageBox.Show("Restoring the default will restart the device. Do you want to continue?", "Info", MessageBoxButtons.OKCancel, MessageBoxIcon.Information);
                if (result == DialogResult.OK)
                {
                    int iRet = NETDEVSDK.NETDEV_RestoreConfig(m_deviceInfoList[DeviceTree.SelectedNode.Index].m_lpDevHandle);
                    if (NETDEVSDK.TRUE != iRet)
                    {
                        showFailLogInfo(m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_ip + " chl:" + (getChannelID()), "Restore Config fail", NETDEVSDK.NETDEV_GetLastError());
                    }
                    else
                    {
                        showSuccessLogInfo(m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_ip + " chl:" + (getChannelID()), "Restore Config");
                        Logout_Click(null, null);
                    }
                }
                else
                {
                    return;
                }
            }
            else
            {
                MessageBox.Show("Cannot restore channel config","warning");
            }
        }

        private void passengerFlowStatisticCallBackFunc(IntPtr lpUserID, IntPtr pstPassengerFlowData, IntPtr lpUserData)
        {
            NETDEV_PASSENGER_FLOW_STATISTIC_DATA_S passengerFlowData = (NETDEV_PASSENGER_FLOW_STATISTIC_DATA_S)Marshal.PtrToStructure(pstPassengerFlowData, typeof(NETDEV_PASSENGER_FLOW_STATISTIC_DATA_S));
           
            String strIP = GetDevIPByDevHandle(lpUserID);
            String strReportTime = getStrTime(passengerFlowData.tReportTime);
  
            ListViewItem item = new ListViewItem(strIP);
            item.SubItems.Add(Convert.ToString(passengerFlowData.dwChannelID));
            item.SubItems.Add(strReportTime);
            item.SubItems.Add(Convert.ToString(passengerFlowData.tInterval));
            item.SubItems.Add(Convert.ToString(passengerFlowData.dwEnterNum));
            item.SubItems.Add(Convert.ToString(passengerFlowData.dwExitNum));
            item.SubItems.Add(Convert.ToString(passengerFlowData.dwTotalEnterNum));
            item.SubItems.Add(Convert.ToString(passengerFlowData.dwTotalExitNum));
            VCAReportDataListView.Items.Add(item);
        }

        private String GetDevIPByDevHandle(IntPtr handle)
        {
            String strDeviceIP = null;
            for (int i = 0; i < m_deviceInfoList.Count; i++)
            {
                if (NETDEMO.NETDEMO_DEVICE_TYPE_E.NETDEMO_DEVICE_INVALID == m_deviceInfoList[i].m_eDeviceType)
                {
                    continue;
                }

                if (m_deviceInfoList[i].m_lpDevHandle == handle)
                {
                    strDeviceIP = m_deviceInfoList[i].m_ip;
                    break;
                }
            }

            if (strDeviceIP == null)
            {
                strDeviceIP = "Unknown ip";
            }

            return strDeviceIP;
        }

        private void VCARegCallBackBtn_Click(object sender, EventArgs e)
        {
            if (m_deviceInfoList.Count == 0)
            {
                return;
            }

            if ((DeviceTree.SelectedNode.ImageIndex == NETDEMO.NETDEV_TREEVIEW_IMAGE_LOCAL_DEVICE_ON
                || DeviceTree.SelectedNode.ImageIndex == NETDEMO.NETDEV_TREEVIEW_IMAGE_CLOUD_DEVICE_ON) 
                && m_deviceInfoList[DeviceTree.SelectedNode.Index].m_lpDevHandle != IntPtr.Zero)
            {
                if (null == passengerCB)
                {
                    passengerCB = new NETDEVSDK.NETDEV_PassengerFlowStatisticCallBack_PF(passengerFlowStatisticCallBackFunc);
                }
                int iRet = NETDEVSDK.NETDEV_SetPassengerFlowStatisticCallBack(m_deviceInfoList[DeviceTree.SelectedNode.Index].m_lpDevHandle, passengerCB, IntPtr.Zero);
                if (NETDEVSDK.TRUE != iRet)
                {
                    showFailLogInfo(m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_ip + " chl:" + (getChannelID()), "Set Passenger Flow Statistic Call Back fail", NETDEVSDK.NETDEV_GetLastError());
                    return;
                }

                showSuccessLogInfo(m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_ip + " chl:" + (getChannelID()), "Set Passenger Flow Statistic Call Back");
            }
            else
            {
                MessageBox.Show("Cannot Set Passenger Flow Statistic Call Back","warning");
            }
        }

        private void VCACloseCallBackBtn_Click(object sender, EventArgs e)
        {
            if (m_deviceInfoList.Count == 0)
            {
                return;
            }

            if ((DeviceTree.SelectedNode.ImageIndex == NETDEMO.NETDEV_TREEVIEW_IMAGE_LOCAL_DEVICE_ON
                || DeviceTree.SelectedNode.ImageIndex == NETDEMO.NETDEV_TREEVIEW_IMAGE_CLOUD_DEVICE_ON)
                && m_deviceInfoList[DeviceTree.SelectedNode.Index].m_lpDevHandle != IntPtr.Zero)
            {
                int iRet = NETDEVSDK.NETDEV_SetPassengerFlowStatisticCallBack(m_deviceInfoList[DeviceTree.SelectedNode.Index].m_lpDevHandle, null, IntPtr.Zero);
                if (NETDEVSDK.TRUE != iRet)
                {
                    showFailLogInfo(m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_ip + " chl:" + (getChannelID()), "UnRegister Passenger Flow Statistic Call back", NETDEVSDK.NETDEV_GetLastError());
                    return;
                }
                showSuccessLogInfo(m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_ip + " chl:" + (getChannelID()), "UnRegister Passenger Flow Statistic Call back");
            }
            else
            {
                MessageBox.Show("UnRegister Passenger Flow Statistic Call back fail","warning");
            }
        }

        private void VCAClearDataBtn_Click(object sender, EventArgs e)
        {
            DialogResult result = MessageBox.Show("Clear the PeopleCounting list?", "Warning", MessageBoxButtons.OKCancel, MessageBoxIcon.Information);
            if (result == DialogResult.OK)
            {
                VCAReportDataListView.Items.Clear();
            }
        }


        private void VCACountBtn_Click(object sender, EventArgs e)
        {
             if (m_deviceInfoList.Count == 0)
            {
                return;
            }

             if (DeviceTree.SelectedNode.ImageIndex == NETDEMO.NETDEV_TREEVIEW_IMAGE_CHL_DEVICE_ON && m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_lpDevHandle != IntPtr.Zero)
             {
                 if (m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_channelNumber == 0)
                 {
                     return;
                 }
                 NETDEV_TRAFFIC_STATISTICS_COND_S stStatisticCond = new NETDEV_TRAFFIC_STATISTICS_COND_S();
                 NETDEV_TRAFFIC_STATISTICS_DATA_S stTrafficStatistic = new NETDEV_TRAFFIC_STATISTICS_DATA_S();
                 stTrafficStatistic.adwEnterCount = new Int32[NETDEVSDK.NETDEV_PEOPLE_CNT_MAX_NUM];
                 stTrafficStatistic.adwExitCount = new Int32[NETDEVSDK.NETDEV_PEOPLE_CNT_MAX_NUM];

                 stStatisticCond.dwChannelID = getChannelID();

                 getTimeInfo(ref stStatisticCond.tBeginTime, ref stStatisticCond.tEndTime);
                 stStatisticCond.dwFormType = VCAReportType.SelectedIndex;
                 stStatisticCond.dwStatisticsType = VCACountingType.SelectedIndex;

                 int iRet = NETDEVSDK.NETDEV_GetTrafficStatistic(m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_lpDevHandle, ref stStatisticCond, ref stTrafficStatistic);
                 if (NETDEVSDK.TRUE != iRet)
                 {
                     showFailLogInfo(m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_ip + " chl:" + (getChannelID()), "Get Traffic Statistic", NETDEVSDK.NETDEV_GetLastError());
                     VCAStatisticDataListView.Items.Clear();
                     return;
                 }
                 else
                 {
                     showSuccessLogInfo(m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_ip + " chl:" + (getChannelID()), "Get Traffic Statistic");
                     displaytheStatistics(ref stTrafficStatistic);
                 }
             }
             else
             {
                 MessageBox.Show("Cannot Get Traffic Statistic","warning");
             }
        }


        private void getTimeInfo(ref Int64 beginTime,ref Int64 endTime)
        {
            Int32 dwYear = VCAStatisticalTime.Value.Year;
            Int32 dwMonth = VCAStatisticalTime.Value.Month;
            Int32 dwDay = VCAStatisticalTime.Value.Day;
            Int32 dwDayOfWeek = 0;
            if (0 == (int)VCAStatisticalTime.Value.DayOfWeek)
            {
                dwDayOfWeek = 7;
            } 
            else
            {
                dwDayOfWeek = (int)VCAStatisticalTime.Value.DayOfWeek;
            }

            switch (VCAReportType.SelectedIndex)
            {
                case (int)NETDEV_FORM_TYPE_E.NETDEV_FORM_TYPE_DAY:
                    beginTime = getLongTime("" + dwYear + "/" + dwMonth + "/" + dwDay);
                    endTime = beginTime + (3600 * 24 - 1);
                    break;

                case (int)NETDEV_FORM_TYPE_E.NETDEV_FORM_TYPE_WEEK:
                    if (dwDay - dwDayOfWeek >= 0)
                    {
                        beginTime = getLongTime("" + dwYear + "/" + dwMonth + "/" + (dwDay - dwDayOfWeek + 1));
                        endTime = beginTime + (3600 * 24 * 7 - 1);
                    }
                    else
                    {
                        dwDay = dwDay - dwDayOfWeek + 1 + DateNumOfMonth(dwYear, dwMonth);
                        beginTime = getLongTime("" + dwYear + "/" + (dwMonth - 1) + "/" + dwDay);
                        endTime = beginTime + (3600 * 24 * 7 - 1);
                    }
                    break;

                case (int)NETDEV_FORM_TYPE_E.NETDEV_FORM_TYPE_MONTH:
                    beginTime = getLongTime("" + dwYear + "/" + dwMonth + "/1"); 
                    endTime = getLongTime("" + dwYear + "/" + (dwMonth + 1) + "/1") - 1;
                    break;

                case (int)NETDEV_FORM_TYPE_E.NETDEV_FORM_TYPE_YEAR:
                    beginTime = getLongTime("" + dwYear + "/1" + "/1");
                    endTime = getLongTime("" + (dwYear + 1) + "/1" + "/1") - 1;
                    break;

                default:
                    break;
            }
        }

        private int DateNumOfMonth(int dwYear, int dwMonth)
        {
            Int32[] aDays = { 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };
            if(((dwYear % 4 == 0)&& (dwYear % 100 != 0))||dwYear % 400 == 0)
            {
                aDays[1]=29;
            }

            return aDays[dwMonth - 1];
        }

        private void displaytheStatistics(ref NETDEV_TRAFFIC_STATISTICS_DATA_S stTrafficStatistic)
        {
            string[] szWeekly = {"Mon", "Tue", "Wed", "Thu" ,"Fri" ,"Sat", "Sun"};
            string[] szYearly = {"Jan", "Feb", "Mar", "Apr" ,"May" ,"Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"};

            VCAStatisticDataListView.Items.Clear();

            for(Int32 i = 0; i < stTrafficStatistic.dwSize; i++)
            {
                ListViewItem item = null;
                if ((0 != stTrafficStatistic.adwEnterCount[i]) || (0 != stTrafficStatistic.adwExitCount[i]))
                {
                    switch(VCAReportType.SelectedIndex)
                    {
                        case (int)NETDEV_FORM_TYPE_E.NETDEV_FORM_TYPE_DAY:
                            item = new ListViewItem("" + i + ":00 - " + (i+1) + ":00" );
                            break;

                        case (int)NETDEV_FORM_TYPE_E.NETDEV_FORM_TYPE_WEEK:
                            item = new ListViewItem("" + szWeekly[i]);
                            break;

                        case (int)NETDEV_FORM_TYPE_E.NETDEV_FORM_TYPE_MONTH:
                            item = new ListViewItem("" + (i + 1));
                            break;

                        case (int)NETDEV_FORM_TYPE_E.NETDEV_FORM_TYPE_YEAR:
                            item = new ListViewItem("" + szYearly[i]);
                            break;

                        default:
                            break;
                    }

                    item.SubItems.Add(Convert.ToString(stTrafficStatistic.adwEnterCount[i]));
                    item.SubItems.Add(Convert.ToString(stTrafficStatistic.adwExitCount[i]));
                    VCAStatisticDataListView.Items.Add(item);
                }
            }

            return;
        }

        public void showFailLogInfo(string deviceInfo, string logInfo, int errorCode)
        {
            ListViewItem item = new ListViewItem(DateTime.Now.ToString());
            item.SubItems.Add(deviceInfo);
            item.SubItems.Add(logInfo);
            item.SubItems.Add("Fail");
            item.SubItems.Add(Convert.ToString(errorCode));
            this.logListView.Items.Insert(0, item);
            item.EnsureVisible();

            LogMessage.failLog(deviceInfo, logInfo, errorCode);
        }

        public void showSuccessLogInfo(string deviceInfo, string logInfo)
        {
            ListViewItem item = new ListViewItem(DateTime.Now.ToString());
            item.SubItems.Add(deviceInfo);
            item.SubItems.Add(logInfo);
            item.SubItems.Add("Success");
            item.SubItems.Add("");
            this.logListView.Items.Insert(0, item);
            item.EnsureVisible();

            LogMessage.sucessLog(deviceInfo, logInfo);
        }

        private void cleanLogBtn_Click(object sender, EventArgs e)
        {
            logListView.Items.Clear();
        }

        
        private void FullScreen_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem toolStripMenuItem = sender as ToolStripMenuItem;
            if (toolStripMenuItem.Checked == false)
            {
                
                this.m_layoutPanelWidth = this.LayoutPanel.Width;
                this.m_layoutPanelHeight = this.LayoutPanel.Height;

                Form form = new Form();
                form.WindowState = FormWindowState.Maximized;
                form.FormBorderStyle = FormBorderStyle.None;
                form.Controls.Add(this.LayoutPanel);
                form.Width = Screen.PrimaryScreen.Bounds.Width - 10;
                form.Height = Screen.PrimaryScreen.Bounds.Height - 10;
                this.LayoutPanel.Width = form.Width;
                this.LayoutPanel.Height = form.Height;
                if (realMaxFlag == true)
                {
                    realMaxFlag = false;
                }
                else
                {
                    realMaxFlag = true;
                }
                switchRealScreen(m_mourseRightSelectedPanel);
                toolStripMenuItem.Checked = true;
                form.ShowDialog();
            }
            else
            {
                toolStripMenuItem.Checked = false;
                Form form = (Form)this.LayoutPanel.Parent;
                this.LayoutPanel.Width = this.m_layoutPanelWidth;
                this.LayoutPanel.Height = this.m_layoutPanelHeight;
                if (realMaxFlag == true)
                {
                    realMaxFlag = false;
                }
                else
                {
                    realMaxFlag = true;
                }
                switchRealScreen(m_mourseRightSelectedPanel);
                this.LiveView.Controls.Add(this.LayoutPanel);

                form.Close();
            }
        }

        
        private void MultiScreen_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem toolStripMenuItem = sender as ToolStripMenuItem;
            if (toolStripMenuItem.Checked == false)
            {
                switchRealScreen(m_mourseRightSelectedPanel);
                toolStripMenuItem.Checked = true;
            }
            else
            {
                toolStripMenuItem.Checked = false;
                switchRealScreen(m_mourseRightSelectedPanel);
            }
        }

        
        private void CameraInfo_Click(object sender, EventArgs e)
        {
            if (m_mourseRightSelectedPanel.m_playhandle == IntPtr.Zero)
            {
                return;
            }

            string strCameraInfo = "";

            int bitRate = -1;
            int iRet = NETDEVSDK.NETDEV_GetBitRate(m_mourseRightSelectedPanel.m_playhandle, ref bitRate);
            if (NETDEVSDK.FALSE == iRet)
            {
                //
            }
            strCameraInfo += "Bit Rate         " + bitRate + "\n";

            int frameRate = -1;
            iRet = NETDEVSDK.NETDEV_GetFrameRate(m_mourseRightSelectedPanel.m_playhandle, ref frameRate);
            if (NETDEVSDK.FALSE == iRet)
            {
                //
            }
            strCameraInfo += "Frame Rate       " + frameRate + "\n";

            int format = -1;
            iRet = NETDEVSDK.NETDEV_GetVideoEncodeFmt(m_mourseRightSelectedPanel.m_playhandle, ref format);
            if (NETDEVSDK.FALSE == iRet)
            {
                //
            }
            strCameraInfo += "Format           " + format + "\n";

            int resolutionWidth = -1;
            int resolutionHeight = -1;
            iRet = NETDEVSDK.NETDEV_GetResolution(m_mourseRightSelectedPanel.m_playhandle, ref resolutionWidth, ref resolutionHeight);
            if (NETDEVSDK.FALSE == iRet)
            {
                //
            }
            strCameraInfo += "Resolution       " + resolutionWidth + " x " + resolutionHeight + "\n";

            int ulRecvPktNum = -1;
            int ulLostPktNum = -1;
            iRet = NETDEVSDK.NETDEV_GetLostPacketRate(m_mourseRightSelectedPanel.m_playhandle, ref ulRecvPktNum, ref ulLostPktNum);
            if (NETDEVSDK.FALSE == iRet)
            {
                //
            }
            strCameraInfo += "Lost Packet Rate " + "(" + ulLostPktNum + "/" + ulRecvPktNum + ")\n";

            MessageBox.Show(strCameraInfo, "Camera Info");
        }

        
        private void CloseAll_Click(object sender, EventArgs e)
        {
            closeAllChannel();
        }

        
        private void Close_Click(object sender, EventArgs e)
        {
            stopRealPlay(m_mourseRightSelectedPanel, true);
            m_mourseRightSelectedPanel.initPlayPanel();
        }

        
        private void ShowDelay_Click(object sender, EventArgs e)
        {
            if (IntPtr.Zero == m_curRealPanel.m_playhandle)
            {
                MessageBox.Show("Set picture fluency, Handle is NULL", "warning");
                return;
            }

            SetNetPlayMode(NETDEV_PICTURE_FLUENCY_E.NETDEV_PICTURE_REAL);

            m_curRealPanel.m_bShortDelayFlag = true;
            m_curRealPanel.m_bFluentFlag = false;
        }

        
        private void Fluent_Click(object sender, EventArgs e)
        {
            if (IntPtr.Zero == m_curRealPanel.m_playhandle)
            {
                MessageBox.Show("Set picture fluency, Handle is NULL", "warning");
                return;
            }

            SetNetPlayMode(NETDEV_PICTURE_FLUENCY_E.NETDEV_PICTURE_FLUENCY);

            m_curRealPanel.m_bShortDelayFlag = false;
            m_curRealPanel.m_bFluentFlag = true;
        }

        private void SetNetPlayMode(NETDEV_PICTURE_FLUENCY_E type)
        {
            if (NETDEVSDK.FALSE == NETDEVSDK.NETDEV_SetPictureFluency(m_curRealPanel.m_playhandle, (Int32)type))
            {
                showFailLogInfo(m_deviceInfoList[m_curRealPanel.m_deviceIndex].m_ip + " chl:" + (m_curRealPanel.m_channelID), "Set picture fluency, type : " + type, NETDEVSDK.NETDEV_GetLastError());
                return;
            }
            showSuccessLogInfo(m_deviceInfoList[m_curRealPanel.m_deviceIndex].m_ip + " chl:" + (m_curRealPanel.m_channelID), "Set picture fluency, type : " + type);
        }


        private void MakeKeyFrame_Click(object sender, EventArgs e)
        {
            if (IntPtr.Zero == m_curRealPanel.m_playhandle)
            {
                MessageBox.Show("make keyFrame, Handle is NULL", "warning");
                return;
            }

            makeKeyFrame();
        }

        private void makeKeyFrame()
        {
            Int32 dwBytesReturned = 0;
            NETDEV_VIDEO_STREAM_INFO_S stStreamInfo = new NETDEV_VIDEO_STREAM_INFO_S();

            stStreamInfo.enStreamType = NETDEV_LIVE_STREAM_INDEX_E.NETDEV_LIVE_STREAM_INDEX_MAIN;

            if (NETDEVSDK.FALSE == NETDEVSDK.NETDEV_GetDevConfig(m_deviceInfoList[m_curRealPanel.m_deviceIndex].m_lpDevHandle, (m_curRealPanel.m_channelID), (int)NETDEV_CONFIG_COMMAND_E.NETDEV_GET_STREAMCFG, ref stStreamInfo, Marshal.SizeOf(stStreamInfo), ref dwBytesReturned))
            {
                showFailLogInfo(m_deviceInfoList[m_curRealPanel.m_deviceIndex].m_ip + " chl:" + (m_curRealPanel.m_channelID), "Get Video Stream Info", NETDEVSDK.NETDEV_GetLastError());
                return;
            }
            showSuccessLogInfo(m_deviceInfoList[m_curRealPanel.m_deviceIndex].m_ip + " chl:" + (m_curRealPanel.m_channelID), "Get Video Stream Info");

            if (NETDEVSDK.FALSE == NETDEVSDK.NETDEV_MakeKeyFrame(m_deviceInfoList[m_curRealPanel.m_deviceIndex].m_lpDevHandle, (m_curRealPanel.m_channelID), (int)stStreamInfo.enStreamType))
            {
                showFailLogInfo(m_deviceInfoList[m_curRealPanel.m_deviceIndex].m_ip + " chl:" + (m_curRealPanel.m_channelID), "Make keyframe", NETDEVSDK.NETDEV_GetLastError());
                return;
            }
            showSuccessLogInfo(m_deviceInfoList[m_curRealPanel.m_deviceIndex].m_ip + " chl:" + (m_curRealPanel.m_channelID), "Make keyframe");

            return;
        }

        
        private void DigitalZoom_Click(object sender, EventArgs e)
        {
            if (IntPtr.Zero == m_curRealPanel.m_playhandle)
            {
                MessageBox.Show("Set Digital zoom, Handle is NULL", "warning");
                return;
            }

            if (true == m_curRealPanel.m_bDigitalZoomFlag)
            {
                m_curRealPanel.m_bDigitalZoomFlag = false;
                m_curRealPanel.m_bFirstZoomFlag = true;
                SetActiveWndDZ(new NETDEV_RECT_S(), false);
            }
            else
            {
                m_curRealPanel.m_bDigitalZoomFlag = true;
            }
        }

        private void SetActiveWndDZ(NETDEV_RECT_S stRect,bool bAction)
        {
            if (true == m_curRealPanel.m_bDigitalZoomFlag && false == m_curRealPanel.m_bFirstZoomFlag)
            {
                return;
            }

            int iRet = NETDEVSDK.FALSE;
            if (true == bAction)
            {
                IntPtr intptrRect = Marshal.AllocHGlobal(Marshal.SizeOf(stRect));   
                Marshal.StructureToPtr(stRect, intptrRect, true); 

                iRet = NETDEVSDK.NETDEV_SetDigitalZoom(m_curRealPanel.m_playhandle, m_curRealPanel.Handle, intptrRect);
                if (NETDEVSDK.TRUE != iRet)
                {
                    showFailLogInfo(m_deviceInfoList[m_curRealPanel.m_deviceIndex].m_ip + " chl:" + (m_curRealPanel.m_channelID), "Set digital zoom", NETDEVSDK.NETDEV_GetLastError());
                }
                else
                {
                    showSuccessLogInfo(m_deviceInfoList[m_curRealPanel.m_deviceIndex].m_ip + " chl:" + (m_curRealPanel.m_channelID), "Set digital zoom"); 
                }
                Marshal.FreeHGlobal(intptrRect);
            }
            else
            {
                iRet = NETDEVSDK.NETDEV_SetDigitalZoom(m_curRealPanel.m_playhandle, m_curRealPanel.Handle, IntPtr.Zero);
                if (NETDEVSDK.TRUE != iRet)
                {
                    showFailLogInfo(m_deviceInfoList[m_curRealPanel.m_deviceIndex].m_ip + " chl:" + (m_curRealPanel.m_channelID), "Set digital zoom", NETDEVSDK.NETDEV_GetLastError());
                    return;
                }
                showSuccessLogInfo(m_deviceInfoList[m_curRealPanel.m_deviceIndex].m_ip + " chl:" + (m_curRealPanel.m_channelID), "Set digital zoom");
            }
        }
         
         private bool convertRect(ref NETDEV_RECT_S dstRect)
        {
            if (m_curRealPanel.rectStartX < m_curRealPanel.rectEndX)
            {
                dstRect.dwLeft = m_curRealPanel.rectStartX;
                dstRect.dwRight = m_curRealPanel.rectEndX;
            }
            else
            {
                dstRect.dwLeft = m_curRealPanel.rectEndX;
                dstRect.dwRight = m_curRealPanel.rectStartX;
            }

            if (m_curRealPanel.rectStartY < m_curRealPanel.rectEndY)
            {
                dstRect.dwTop = m_curRealPanel.rectStartY;
                dstRect.dwBottom = m_curRealPanel.rectEndY;
            }
            else
            {
                dstRect.dwTop = m_curRealPanel.rectEndY;
                dstRect.dwBottom = m_curRealPanel.rectStartY;
            }

            dstRect.dwLeft = (dstRect.dwLeft * 10000) / m_curRealPanel.Width;
            dstRect.dwRight = (dstRect.dwRight * 10000) / m_curRealPanel.Width;
            dstRect.dwTop = (dstRect.dwTop * 10000) / m_curRealPanel.Height;
            dstRect.dwBottom = (dstRect.dwBottom * 10000) / m_curRealPanel.Height;

            int nIntervalX = m_curRealPanel.rectStartX < m_curRealPanel.rectEndX ? (m_curRealPanel.rectEndX - m_curRealPanel.rectStartX) : (m_curRealPanel.rectStartX - m_curRealPanel.rectEndX);
            int nIntervalY = m_curRealPanel.rectStartY < m_curRealPanel.rectEndY ? (m_curRealPanel.rectEndY - m_curRealPanel.rectStartY) : (m_curRealPanel.rectStartY - m_curRealPanel.rectEndY);
            if ( nIntervalX < 20 || nIntervalY <20)
            {
                return false;
            }

            return true;
        }

        private void realPanel_MouseDown(object sender, MouseEventArgs e)
        {
            (sender as PlayPanel).rectStartX = e.X;
            (sender as PlayPanel).rectStartY = e.Y;
        }

        private void realPanel_MouseUp(object sender, MouseEventArgs e)
        {
            if (m_curRealPanel.m_playhandle == IntPtr.Zero)
            {
                return;
            }

            PlayPanel panel = sender as PlayPanel;
            panel.rectEndX = e.X;
            panel.rectEndY = e.Y;
            Graphics g = m_curRealPanel.CreateGraphics();
            g.DrawRectangle(new Pen(Color.Red), panel.rectStartX, panel.rectStartY, panel.rectEndX - panel.rectStartX, panel.rectEndY - panel.rectStartY);

            NETDEV_RECT_S rect = new NETDEV_RECT_S();
            if (false == convertRect(ref rect))
            {
                return;
            }

            if (m_curRealPanel.m_bDigitalZoomFlag == true)
            {
                SetActiveWndDZ(rect, true);
                m_curRealPanel.m_bFirstZoomFlag = false;
            }

            setActive3DPostion(rect);
        }

        private void realPanel_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            PlayPanel panel = sender as PlayPanel;
            panel.rectEndX = e.X;
            panel.rectEndY = e.Y;

            Graphics g = m_curRealPanel.CreateGraphics();
            g.DrawRectangle(new Pen(Color.Red), panel.rectStartX, panel.rectStartY, panel.rectEndX - panel.rectStartX, panel.rectEndY - panel.rectStartY);
        }

        /*3D Position*/
        private void ThreeDPosition_Click(object sender, EventArgs e)
        {
            if (IntPtr.Zero == m_curRealPanel.m_playhandle)
            {
                MessageBox.Show("3D Position, Handle is NULL", "warning");
                return;
            }

            if (true == m_curRealPanel.m_3DPositionFlag)
            {
                //ThreeDPosition.Checked = false;
                m_curRealPanel.m_3DPositionFlag = false;
            }
            else
            {
                //ThreeDPosition.Checked = true;
                m_curRealPanel.m_3DPositionFlag = true;
            }
        }

        private void setActive3DPostion(NETDEV_RECT_S oRect)
        {
            if (true != m_curRealPanel.m_3DPositionFlag)
            {
                return;
            }

            NETDEV_PTZ_OPERATEAREA_S stPtzAreaOperate = new NETDEV_PTZ_OPERATEAREA_S();

            /* Take the upper left corner of the window for playing as a start point, and the parameter value range from 0 to 10000. */
            stPtzAreaOperate.dwBeginPointX = oRect.dwLeft;
            stPtzAreaOperate.dwBeginPointY = oRect.dwTop;
            stPtzAreaOperate.dwEndPointX = oRect.dwRight;
            stPtzAreaOperate.dwEndPointY = oRect.dwBottom;

            int iRet = NETDEVSDK.NETDEV_PTZSelZoomIn_Other(m_deviceInfoList[m_curRealPanel.m_deviceIndex].m_lpDevHandle, m_curRealPanel.m_channelID, ref stPtzAreaOperate);
            if (NETDEVSDK.TRUE != iRet)
            {
                showFailLogInfo(m_deviceInfoList[m_curRealPanel.m_deviceIndex].m_ip + " chl:" + (m_curRealPanel.m_channelID), "Operate area", NETDEVSDK.NETDEV_GetLastError());
                return;
            }
            showSuccessLogInfo(m_deviceInfoList[m_curRealPanel.m_deviceIndex].m_ip + " chl:" + (m_curRealPanel.m_channelID), "Operate area");
        }

        
        private void TwoWayAudio_Click(object sender, EventArgs e)
        {
            if (IntPtr.Zero == m_curRealPanel.m_playhandle)
            {
                MessageBox.Show("Two-way Audio, Handle is NULL", "warning");
                return;
            }

            if (true == m_curRealPanel.m_twoWayAudioFlag)
            {
                if (true == stopTalk())
                {
                    //TwoWayAudio.Checked = false;
                    m_curRealPanel.m_twoWayAudioFlag = false;
                }
                else
                {
                    //TwoWayAudio.Checked = true;
                    m_curRealPanel.m_twoWayAudioFlag = true;
                }
            }
            else
            {
                if (true == startTwoWayAudio())
                {
                    //TwoWayAudio.Checked = true;
                    m_curRealPanel.m_twoWayAudioFlag = true;
                }
                else
                {
                    //TwoWayAudio.Checked = false;
                    m_curRealPanel.m_twoWayAudioFlag = false;
                }
            }
        }

        private bool stopTalk()
        {
            if (IntPtr.Zero == m_curRealPanel.m_talkHandle)
            {
                MessageBox.Show("Stop two way audio, Handle is NULL","warning");
                return false;
            }
            int iRet = NETDEVSDK.NETDEV_StopVoiceCom(m_curRealPanel.m_talkHandle);
            if (NETDEVSDK.TRUE != iRet)
            {
                showFailLogInfo(m_deviceInfoList[m_curRealPanel.m_deviceIndex].m_ip + " chl:" + (m_curRealPanel.m_channelID), "Stop two way audio", NETDEVSDK.NETDEV_GetLastError());
                return false;
            }
            else
            {
                showSuccessLogInfo(m_deviceInfoList[m_curRealPanel.m_deviceIndex].m_ip + " chl:" + (m_curRealPanel.m_channelID), "Stop two way audio");
                m_curRealPanel.m_talkHandle = IntPtr.Zero;
                return true;
            }
        }

        private bool startTwoWayAudio()
        {
            IntPtr handle = m_deviceInfoList[m_curRealPanel.m_deviceIndex].m_lpDevHandle;
            m_curRealPanel.m_talkHandle = NETDEVSDK.NETDEV_StartVoiceCom(handle, m_curRealPanel.m_channelID, IntPtr.Zero, IntPtr.Zero);
            if (IntPtr.Zero == m_curRealPanel.m_talkHandle)
            {
                showFailLogInfo(m_deviceInfoList[m_curRealPanel.m_deviceIndex].m_ip + " chl:" + (m_curRealPanel.m_channelID), "Start two way audio", NETDEVSDK.NETDEV_GetLastError());
                return false;
            }
            else
            {
                showSuccessLogInfo(m_deviceInfoList[m_curRealPanel.m_deviceIndex].m_ip + " chl:" + (m_curRealPanel.m_channelID), "Start two way audio");
                return true;
            }
        }

        private long getLongTime(String strTime)
        {
            DateTime dateTime = Convert.ToDateTime(strTime);
            System.DateTime startTime = TimeZone.CurrentTimeZone.ToLocalTime(new System.DateTime(1970, 1, 1)); // 
            return (long)(dateTime - startTime).TotalSeconds; // 
        }

        private string getStrTime(long time)
        {
            DateTime startDateTime = TimeZone.CurrentTimeZone.ToLocalTime(new System.DateTime(1970, 1, 1)); // 
            return startDateTime.AddSeconds(time).ToString("yyyy/MM/dd HH:mm:ss");

        }

        public string GetDefaultString(byte[] utf8String)
        {
            utf8String = Encoding.Convert(Encoding.GetEncoding("UTF-8"), Encoding.Unicode, utf8String);
            string strUnicode = Encoding.Unicode.GetString(utf8String);
            strUnicode = strUnicode.Substring(0, strUnicode.IndexOf('\0'));
            return strUnicode;
        }

        public void GetUTF8Buffer(string inputString, int bufferLen, out byte[] utf8Buffer)
        {
            utf8Buffer = new byte[bufferLen];
            byte[] tempBuffer = System.Text.Encoding.UTF8.GetBytes(inputString);
            for (int i = 0; i < tempBuffer.Length; ++i)
            {
                utf8Buffer[i] = tempBuffer[i];
            }
        }

        private void inputPcmBtn_Click(object sender, EventArgs e)
        {
            if (m_CurSelectTreeNodeInfo.dwDeviceIndex == -1)
            {
                return;
            }

            int dwChannelID = m_CurSelectTreeNodeInfo.dwChannelID;
            IntPtr lpHandle = m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_lpDevHandle;


            if (IntPtr.Zero == lpHandle)
            {
                MessageBox.Show("Device Handle is 0 ", "warning");
                return;
            }

            IntPtr lpPlayHandle = IntPtr.Zero;
            Int32 bRet = 0;
            lpPlayHandle = NETDEVSDK.NETDEV_StartInputVoiceSrv(lpHandle, dwChannelID);
            if (IntPtr.Zero == lpPlayHandle)
            {
                showFailLogInfo(m_deviceInfoList[m_curRealPanel.m_deviceIndex].m_ip + " chl:" + dwChannelID, "Start Input Voice failed", NETDEVSDK.NETDEV_GetLastError());
                return;
            }
            else
            {
                showSuccessLogInfo(m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_ip + " chl:" + dwChannelID, "Start Input Voice success");
            }
            FileStream fs = new FileStream(@"..\demo\C#\NetDemo\8k16bits.pcm", FileMode.Open);//文件路径写死为相对路径
            BinaryReader sr = new BinaryReader(fs);
            int dwBufLen = 640; //单次发送长度，160的倍数
            NETDEV_AUDIO_SAMPLE_PARAM_S stVoiceParam = new NETDEV_AUDIO_SAMPLE_PARAM_S(); //NETDEV_InputVoiceData第四个参数可以传NULL，为通过C#入参校验，声明一个临时变量
            byte[] szBuf = new byte[dwBufLen];
            while (0 != sr.Read(szBuf, 0, dwBufLen))
            {
                bRet = NETDEVSDK.NETDEV_InputVoiceData(lpPlayHandle, szBuf, dwBufLen, ref stVoiceParam);
                if (0 == bRet)
                {
                    sr.Close();
                    showFailLogInfo(m_deviceInfoList[m_curRealPanel.m_deviceIndex].m_ip + " chl:" + dwChannelID, "NETDEV_InputVoiceData failed", NETDEVSDK.NETDEV_GetLastError());
                    return;
                }
                Thread.Sleep(40);// 根据采样频率，位宽，单次发送长度，计算延时时间
                Array.Clear(szBuf, 0, dwBufLen);
            }

            sr.Close();
            bRet = NETDEVSDK.NETDEV_StopInputVoiceSrv(lpPlayHandle);
            if (0 == bRet)
            {
                showFailLogInfo(m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_ip + " chl:" + dwChannelID, "NETDEV_StopInputVoiceSrv failed", NETDEVSDK.NETDEV_GetLastError());
                return;
            }
            else
            {
                showSuccessLogInfo(m_deviceInfoList[m_CurSelectTreeNodeInfo.dwDeviceIndex].m_ip + " chl:" + dwChannelID, "NETDEV_StopInputVoiceSrv success");
            }

            return;
        }

        private void buttonConnectCamera_Click(object sender, EventArgs e)
        {
            GeneralDef.NETDEMO.NETDEMO_DEVICE_TYPE_E eDeviceType = GeneralDef.NETDEMO.NETDEMO_DEVICE_TYPE_E.NETDEMO_DEVICE_IPC_OR_NVR;


            String strIPAddr = textBoxCamIp.Text.ToString();
            short sPort = 0;
            try
            {
                sPort = 80;
            }
            catch (FormatException)
            {
                return;
            }
            catch (OverflowException)
            {
                return;
            }

            String strUserName = "admin";
            String strPassword = "admin";

            AddLocalDevice(strIPAddr, sPort, strUserName, strPassword, eDeviceType);
            
        
    }

        private void NetDemo_Load(object sender, EventArgs e)
        {

        }

        private void textBoxCamIp_TextChanged(object sender, EventArgs e)
        {

        }
    }
}
