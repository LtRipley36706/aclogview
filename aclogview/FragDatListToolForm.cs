using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

using aclogview.Properties;
using static CM_Physics;

namespace aclogview
{
    public partial class FragDatListToolForm : Form
    {
        public FragDatListToolForm()
        {
            InitializeComponent();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            txtOutputFolder.Text = Settings.Default.FragDatFileOutputFolder;
            txtSearchPathRoot.Text = Settings.Default.FindOpcodeInFilesRoot;
            txtFileToProcess.Text = Settings.Default.FragDatFileToProcess;

            // Center to our owner, if we have one
            if (Owner != null)
                Location = new Point(Owner.Location.X + Owner.Width / 2 - Width / 2, Owner.Location.Y + Owner.Height / 2 - Height / 2);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            searchAborted = true;

            Settings.Default.FragDatFileOutputFolder = txtOutputFolder.Text;
            Settings.Default.FindOpcodeInFilesRoot = txtSearchPathRoot.Text;
            Settings.Default.FragDatFileToProcess = txtFileToProcess.Text;

            base.OnClosing(e);
        }

        private void btnChangeOutputFolder_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog openFolder = new FolderBrowserDialog())
            {
                if (openFolder.ShowDialog() == DialogResult.OK)
                    txtOutputFolder.Text = openFolder.SelectedPath;
            }
        }

        private void btnChangeSearchPathRoot_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog openFolder = new FolderBrowserDialog())
            {
                if (openFolder.ShowDialog() == DialogResult.OK)
                    txtSearchPathRoot.Text = openFolder.SelectedPath;
            }
        }

        private void btnChangeFileToProcess_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFile = new OpenFileDialog())
            {
                openFile.Filter = "Frag Dat List (*.frags)|*.frags";
                openFile.DefaultExt = ".frags";

                if (openFile.ShowDialog() == DialogResult.OK)
                    txtFileToProcess.Text = openFile.FileName;
            }
        }


        private readonly List<string> filesToProcess = new List<string>();
        private int filesProcessed;
        private int fragmentsProcessed;
        private int totalHits;
        private int totalExceptions;
        private bool searchAborted;

        private void ResetVariables()
        {
            filesToProcess.Clear();
            filesProcessed = 0;
            fragmentsProcessed = 0;
            totalHits = 0;
            totalExceptions = 0;
            searchAborted = false;
        }


        private void btnStartBuild_Click(object sender, EventArgs e)
        {
            if (!Directory.Exists(txtOutputFolder.Text))
            {
                MessageBox.Show("Output folder does not exist.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                btnStartBuild.Enabled = false;
                groupBoxGeneralSettings.Enabled = false;
                groupBoxProcessFragDatListFile.Enabled = false;

                ResetVariables();

                filesToProcess.AddRange(Directory.GetFiles(txtSearchPathRoot.Text, "*.pcap", SearchOption.AllDirectories));
                filesToProcess.AddRange(Directory.GetFiles(txtSearchPathRoot.Text, "*.pcapng", SearchOption.AllDirectories));

                txtSearchPathRoot.Enabled = false;
                btnChangeSearchPathRoot.Enabled = false;
                chkCompressOutput.Enabled = false;
                chkIncludeFullPathAndFileName.Enabled = false;
                btnStopBuild.Enabled = true;

                timer1.Start();

                new Thread(() =>
                {
                    // Do the actual work here
                    DoBuild();

                    if (!Disposing && !IsDisposed)
                        btnStopBuild.BeginInvoke((Action)(() => btnStopBuild_Click(null, null)));
                }).Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());

                btnStopBuild_Click(null, null);
            }
        }

        private void btnStopBuild_Click(object sender, EventArgs e)
        {
            searchAborted = true;

            timer1.Stop();

            timer1_Tick(null, null);

            txtSearchPathRoot.Enabled = true;
            btnChangeSearchPathRoot.Enabled = true;
            chkCompressOutput.Enabled = true;
            chkIncludeFullPathAndFileName.Enabled = true;
            btnStartBuild.Enabled = true;
            btnStopBuild.Enabled = false;

            groupBoxGeneralSettings.Enabled = true;
            groupBoxProcessFragDatListFile.Enabled = true;
        }


        // ********************************************************************
        // *************************** Sample Files *************************** 
        // ********************************************************************
        private readonly FragDatListFile allFragDatFile = new FragDatListFile();
        private readonly FragDatListFile createObjectFragDatFile = new FragDatListFile();
        private readonly FragDatListFile appraisalInfoFragDatFile = new FragDatListFile();
        private readonly FragDatListFile bookFragDatFile = new FragDatListFile();
        private readonly FragDatListFile vendorFragDatFile = new FragDatListFile();
        private readonly FragDatListFile slumlordFragDatFile = new FragDatListFile();
        private readonly FragDatListFile aceWorldFragDatFile = new FragDatListFile();

        private void DoBuild()
        {
            // ********************************************************************
            // ************************ Adjust These Paths ************************ 
            // ********************************************************************
            allFragDatFile.CreateFile(Path.Combine(txtOutputFolder.Text, "All.frags"), chkCompressOutput.Checked ? FragDatListFile.CompressionType.DeflateStream : FragDatListFile.CompressionType.None);
            createObjectFragDatFile.CreateFile(Path.Combine(txtOutputFolder.Text, "CreateObject.frags"), chkCompressOutput.Checked ? FragDatListFile.CompressionType.DeflateStream : FragDatListFile.CompressionType.None);
            appraisalInfoFragDatFile.CreateFile(Path.Combine(txtOutputFolder.Text, "AppraisalInfo.frags"), chkCompressOutput.Checked ? FragDatListFile.CompressionType.DeflateStream : FragDatListFile.CompressionType.None);
            bookFragDatFile.CreateFile(Path.Combine(txtOutputFolder.Text, "Book.frags"), chkCompressOutput.Checked ? FragDatListFile.CompressionType.DeflateStream : FragDatListFile.CompressionType.None);
            vendorFragDatFile.CreateFile(Path.Combine(txtOutputFolder.Text, "Vendor.frags"), chkCompressOutput.Checked ? FragDatListFile.CompressionType.DeflateStream : FragDatListFile.CompressionType.None);
            slumlordFragDatFile.CreateFile(Path.Combine(txtOutputFolder.Text, "SlumLord.frags"), chkCompressOutput.Checked ? FragDatListFile.CompressionType.DeflateStream : FragDatListFile.CompressionType.None);
            aceWorldFragDatFile.CreateFile(Path.Combine(txtOutputFolder.Text, "ACE-World.frags"), chkCompressOutput.Checked ? FragDatListFile.CompressionType.DeflateStream : FragDatListFile.CompressionType.None);

            // Do not parallel this search
            foreach (var currentFile in filesToProcess)
            {
                if (searchAborted || Disposing || IsDisposed)
                    break;

                try
                {
                    ProcessFileForBuild(currentFile);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("File failed to process with exception: " + Environment.NewLine + ex, "Exception", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            // ********************************************************************
            // ****************************** Cleanup ***************************** 
            // ********************************************************************
            allFragDatFile.CloseFile();
            createObjectFragDatFile.CloseFile();
            appraisalInfoFragDatFile.CloseFile();
            bookFragDatFile.CloseFile();
            vendorFragDatFile.CloseFile();
            slumlordFragDatFile.CloseFile();
            aceWorldFragDatFile.CloseFile();
        }

        private void ProcessFileForBuild(string fileName)
        {
            // NOTE: If you want to get fully constructed/merged messages isntead of fragments:
            // Pass true below and use record.data as the full message, instead of individual record.frags
            //var records = PCapReader.LoadPcap(fileName, false, ref searchAborted);
            var records = PCapReader.LoadPcap(fileName, true, ref searchAborted);

            // Temperorary objects
            var allFrags = new List<FragDatListFile.FragDatInfo>();
            var createObjectFrags = new List<FragDatListFile.FragDatInfo>();
            var appraisalInfoFrags = new List<FragDatListFile.FragDatInfo>();
            var bookFrags = new List<FragDatListFile.FragDatInfo>();
            var vendorFrags = new List<FragDatListFile.FragDatInfo>();
            var slumlordFrags = new List<FragDatListFile.FragDatInfo>();
            var aceWorldFrags = new List<FragDatListFile.FragDatInfo>();

            foreach (var record in records)
            {
                if (searchAborted || Disposing || IsDisposed)
                    return;

                // ********************************************************************
                // ************************ Custom Search Code ************************ 
                // ********************************************************************
                //foreach (BlobFrag frag in record.frags)
                //{
                try
                {
                    //if (frag.dat_.Length <= 4)
                    //continue;

                    Interlocked.Increment(ref fragmentsProcessed);

                    FragDatListFile.PacketDirection packetDirection = (record.isSend ? FragDatListFile.PacketDirection.ClientToServer : FragDatListFile.PacketDirection.ServerToClient);

                    // Write to emperorary object
                    //allFrags.Add(new FragDatListFile.FragDatInfo(packetDirection, record.index, frag.dat_));
                    allFrags.Add(new FragDatListFile.FragDatInfo(packetDirection, record.index, record.data));

                    //BinaryReader fragDataReader = new BinaryReader(new MemoryStream(frag.dat_));
                    BinaryReader fragDataReader = new BinaryReader(new MemoryStream(record.data));

                    var messageCode = fragDataReader.ReadUInt32();

                    // Write to emperorary object
                    if (messageCode == (uint)PacketOpcode.Evt_Physics__CreateObject_ID)
                    {
                        Interlocked.Increment(ref totalHits);

                        //createObjectFrags.Add(new FragDatListFile.FragDatInfo(packetDirection, record.index, frag.dat_));
                        createObjectFrags.Add(new FragDatListFile.FragDatInfo(packetDirection, record.index, record.data));

                        //createObjectPlusAppraisalInfoFrags.Add(new FragDatListFile.FragDatInfo(packetDirection, record.index, frag.dat_));
                        aceWorldFrags.Add(new FragDatListFile.FragDatInfo(packetDirection, record.index, record.data));
                    }

                    if (messageCode == (uint)PacketOpcode.APPRAISAL_INFO_EVENT)
                    {
                        Interlocked.Increment(ref totalHits);

                        //appraisalInfoFrags.Add(new FragDatListFile.FragDatInfo(packetDirection, record.index, frag.dat_));
                        appraisalInfoFrags.Add(new FragDatListFile.FragDatInfo(packetDirection, record.index, record.data));

                        //createObjectPlusAppraisalInfoFrags.Add(new FragDatListFile.FragDatInfo(packetDirection, record.index, frag.dat_));
                        aceWorldFrags.Add(new FragDatListFile.FragDatInfo(packetDirection, record.index, record.data));
                    }

                    if (messageCode == (uint)PacketOpcode.BOOK_DATA_RESPONSE_EVENT)
                    {
                        Interlocked.Increment(ref totalHits);

                        bookFrags.Add(new FragDatListFile.FragDatInfo(packetDirection, record.index, record.data));

                        aceWorldFrags.Add(new FragDatListFile.FragDatInfo(packetDirection, record.index, record.data));
                    }

                    if (messageCode == (uint)PacketOpcode.BOOK_PAGE_DATA_RESPONSE_EVENT)
                    {
                        Interlocked.Increment(ref totalHits);

                        bookFrags.Add(new FragDatListFile.FragDatInfo(packetDirection, record.index, record.data));

                        aceWorldFrags.Add(new FragDatListFile.FragDatInfo(packetDirection, record.index, record.data));
                    }

                    if (messageCode == (uint)PacketOpcode.Evt_Writing__BookData_ID)
                    {
                        Interlocked.Increment(ref totalHits);

                        bookFrags.Add(new FragDatListFile.FragDatInfo(packetDirection, record.index, record.data));

                        aceWorldFrags.Add(new FragDatListFile.FragDatInfo(packetDirection, record.index, record.data));
                    }

                    if (messageCode == (uint)PacketOpcode.Evt_Writing__BookPageData_ID)
                    {
                        Interlocked.Increment(ref totalHits);

                        bookFrags.Add(new FragDatListFile.FragDatInfo(packetDirection, record.index, record.data));

                        aceWorldFrags.Add(new FragDatListFile.FragDatInfo(packetDirection, record.index, record.data));
                    }

                    if (messageCode == (uint)PacketOpcode.VENDOR_INFO_EVENT)
                    {
                        Interlocked.Increment(ref totalHits);

                        vendorFrags.Add(new FragDatListFile.FragDatInfo(packetDirection, record.index, record.data));

                        aceWorldFrags.Add(new FragDatListFile.FragDatInfo(packetDirection, record.index, record.data));
                    }

                    if (messageCode == (uint)PacketOpcode.Evt_House__Recv_HouseProfile_ID
                        || messageCode == (uint)PacketOpcode.Evt_House__Recv_HouseData_ID
                        )
                    {
                        Interlocked.Increment(ref totalHits);

                        slumlordFrags.Add(new FragDatListFile.FragDatInfo(packetDirection, record.index, record.data));

                        aceWorldFrags.Add(new FragDatListFile.FragDatInfo(packetDirection, record.index, record.data));
                    }

                    if (messageCode == (uint)PacketOpcode.WEENIE_ORDERED_EVENT || messageCode == (uint)PacketOpcode.ORDERED_EVENT) 
                    {
                        uint opCode = 0;

                        if (messageCode == (uint)PacketOpcode.WEENIE_ORDERED_EVENT)
                        {
                            WOrderHdr orderHeader = WOrderHdr.read(fragDataReader);
                            opCode = fragDataReader.ReadUInt32();
                        }
                        if (messageCode == (uint)PacketOpcode.ORDERED_EVENT)
                        {
                            OrderHdr orderHeader = OrderHdr.read(fragDataReader);
                            opCode = fragDataReader.ReadUInt32();
                        }

                        if (opCode == (uint)PacketOpcode.Evt_Physics__CreateObject_ID)
                        {
                            Interlocked.Increment(ref totalHits);

                            //createObjectFrags.Add(new FragDatListFile.FragDatInfo(packetDirection, record.index, frag.dat_));
                            createObjectFrags.Add(new FragDatListFile.FragDatInfo(packetDirection, record.index, record.data));

                            //createObjectPlusAppraisalInfoFrags.Add(new FragDatListFile.FragDatInfo(packetDirection, record.index, frag.dat_));
                            aceWorldFrags.Add(new FragDatListFile.FragDatInfo(packetDirection, record.index, record.data));
                        }

                        if (opCode == (uint)PacketOpcode.APPRAISAL_INFO_EVENT)
                        {
                            Interlocked.Increment(ref totalHits);

                            //appraisalInfoFrags.Add(new FragDatListFile.FragDatInfo(packetDirection, record.index, frag.dat_));
                            appraisalInfoFrags.Add(new FragDatListFile.FragDatInfo(packetDirection, record.index, record.data));

                            //createObjectPlusAppraisalInfoFrags.Add(new FragDatListFile.FragDatInfo(packetDirection, record.index, frag.dat_));
                            aceWorldFrags.Add(new FragDatListFile.FragDatInfo(packetDirection, record.index, record.data));
                        }

                        if (opCode == (uint)PacketOpcode.BOOK_DATA_RESPONSE_EVENT)
                        {
                            Interlocked.Increment(ref totalHits);

                            bookFrags.Add(new FragDatListFile.FragDatInfo(packetDirection, record.index, record.data));

                            aceWorldFrags.Add(new FragDatListFile.FragDatInfo(packetDirection, record.index, record.data));
                        }

                        if (opCode == (uint)PacketOpcode.BOOK_PAGE_DATA_RESPONSE_EVENT)
                        {
                            Interlocked.Increment(ref totalHits);

                            bookFrags.Add(new FragDatListFile.FragDatInfo(packetDirection, record.index, record.data));

                            aceWorldFrags.Add(new FragDatListFile.FragDatInfo(packetDirection, record.index, record.data));
                        }

                        if (opCode == (uint)PacketOpcode.Evt_Writing__BookData_ID)
                        {
                            Interlocked.Increment(ref totalHits);

                            bookFrags.Add(new FragDatListFile.FragDatInfo(packetDirection, record.index, record.data));

                            aceWorldFrags.Add(new FragDatListFile.FragDatInfo(packetDirection, record.index, record.data));
                        }

                        if (opCode == (uint)PacketOpcode.Evt_Writing__BookPageData_ID)
                        {
                            Interlocked.Increment(ref totalHits);

                            bookFrags.Add(new FragDatListFile.FragDatInfo(packetDirection, record.index, record.data));

                            aceWorldFrags.Add(new FragDatListFile.FragDatInfo(packetDirection, record.index, record.data));
                        }

                        if (opCode == (uint)PacketOpcode.VENDOR_INFO_EVENT)
                        {
                            Interlocked.Increment(ref totalHits);

                            vendorFrags.Add(new FragDatListFile.FragDatInfo(packetDirection, record.index, record.data));

                            aceWorldFrags.Add(new FragDatListFile.FragDatInfo(packetDirection, record.index, record.data));
                        }

                        if (opCode == (uint)PacketOpcode.Evt_House__Recv_HouseProfile_ID 
                            || opCode == (uint)PacketOpcode.Evt_House__Recv_HouseData_ID
                            )
                        {
                            Interlocked.Increment(ref totalHits);

                            slumlordFrags.Add(new FragDatListFile.FragDatInfo(packetDirection, record.index, record.data));

                            aceWorldFrags.Add(new FragDatListFile.FragDatInfo(packetDirection, record.index, record.data));
                        }
                    }
                }
                catch
                {
                    // Do something with the exception maybe
                    Interlocked.Increment(ref totalExceptions);
                }
                //}
            }

            string outputFileName = (chkIncludeFullPathAndFileName.Checked ? fileName : (Path.GetFileName(fileName)));

            // ********************************************************************
            // ************************* Write The Output ************************* 
            // ********************************************************************
            allFragDatFile.Write(new KeyValuePair<string, IList<FragDatListFile.FragDatInfo>>(outputFileName, allFrags));
            createObjectFragDatFile.Write(new KeyValuePair<string, IList<FragDatListFile.FragDatInfo>>(outputFileName, createObjectFrags));
            appraisalInfoFragDatFile.Write(new KeyValuePair<string, IList<FragDatListFile.FragDatInfo>>(outputFileName, appraisalInfoFrags));
            bookFragDatFile.Write(new KeyValuePair<string, IList<FragDatListFile.FragDatInfo>>(outputFileName, bookFrags));
            vendorFragDatFile.Write(new KeyValuePair<string, IList<FragDatListFile.FragDatInfo>>(outputFileName, vendorFrags));
            slumlordFragDatFile.Write(new KeyValuePair<string, IList<FragDatListFile.FragDatInfo>>(outputFileName, slumlordFrags));
            aceWorldFragDatFile.Write(new KeyValuePair<string, IList<FragDatListFile.FragDatInfo>>(outputFileName, aceWorldFrags));

            Interlocked.Increment(ref filesProcessed);
        }


        private void btnStartProcess_Click(object sender, EventArgs e)
        {
            try
            {
                btnStartProcess.Enabled = false;
                groupBoxGeneralSettings.Enabled = false;
                groupBoxFragDatListFileBuilder.Enabled = false;

                ResetVariables();

                filesToProcess.Add(txtFileToProcess.Text);

                txtFileToProcess.Enabled = false;
                btnChangeFileToProcess.Enabled = false;
                btnStopProcess.Enabled = true;

                timer1.Start();

                new Thread(() =>
                {
                    // Do the actual work here
                    DoProcess();

                    if (!Disposing && !IsDisposed)
                        btnStopProcess.BeginInvoke((Action)(() => btnStopProcess_Click(null, null)));
                }).Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());

                btnStopProcess_Click(null, null);
            }
        }

        private void btnStopProcess_Click(object sender, EventArgs e)
        {
            searchAborted = true;

            timer1.Stop();

            timer1_Tick(null, null);

            txtFileToProcess.Enabled = true;
            btnChangeFileToProcess.Enabled = true;
            btnStartProcess.Enabled = true;
            btnStopProcess.Enabled = false;

            groupBoxGeneralSettings.Enabled = true;
            groupBoxFragDatListFileBuilder.Enabled = true;
        }

        private void DoProcess()
        {
            // Do not parallel this search
            foreach (var currentFile in filesToProcess)
            {
                if (searchAborted || Disposing || IsDisposed)
                    break;

                try
                {
                    ProcessFileForExamination(currentFile);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("File failed to process with exception: " + Environment.NewLine + ex, "Exception", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ProcessFileForExamination(string fileName)
        {
            var fragDatListFile = new FragDatListFile();
            DateTime start = DateTime.Now;

            if (!fragDatListFile.OpenFile(fileName))
                return;

            var itemTypesToParse = new List<ITEM_TYPE>();

            var itemTypeKeys = new Dictionary<ITEM_TYPE, List<string>>();
            //var itemTypeStreamWriters = new Dictionary<ITEM_TYPE, StreamWriter>();

            // If you only want to output a single item_type, you can change this code
            foreach (ITEM_TYPE itemType in Enum.GetValues(typeof(ITEM_TYPE)))
            {
                itemTypesToParse.Add(itemType);
                itemTypeKeys[itemType] = new List<string>();
                //itemTypeStreamWriters[itemType] = new StreamWriter(Path.Combine(txtOutputFolder.Text, itemType + ".csv.temp"));
            }

            try
            {
                TreeView treeView = new TreeView();

                List<uint> weenieIds = new List<uint>();
                Dictionary<string, List<CM_Physics.CreateObject>> weenies = new Dictionary<string, List<CM_Physics.CreateObject>>();
                Dictionary<uint, List<Position>> processedWeeniePositions = new Dictionary<uint, List<Position>>();

                List<uint> objectIds = new List<uint>();
                Dictionary<string, List<CM_Physics.CreateObject>> staticObjects = new Dictionary<string, List<CM_Physics.CreateObject>>();

                bool outputAsLandblocks = true;
                bool useLandblockTable = true;
                bool exportEverything = false;
                bool generateNewGuids = false;
                bool dedupeWeenies = true;
                bool outputWeenieFiles = true;
                Dictionary<uint, Dictionary<string, List<CM_Physics.CreateObject>>> landblockInstances = new Dictionary<uint, Dictionary<string, List<CM_Physics.CreateObject>>>();

                Dictionary<uint, List<uint>> wieldedObjectsParentMap = new Dictionary<uint, List<uint>>();
                Dictionary<uint, List<CM_Physics.CreateObject>> wieldedObjects = new Dictionary<uint, List<CM_Physics.CreateObject>>();

                Dictionary<uint, List<uint>> inventoryParents = new Dictionary<uint, List<uint>>();
                Dictionary<uint, CM_Physics.CreateObject> inventoryObjects = new Dictionary<uint, CM_Physics.CreateObject>();
                Dictionary<uint, List<uint>> parentWieldsWeenies = new Dictionary<uint, List<uint>>();

                List<uint> appraisalObjectIds = new List<uint>();
                Dictionary<string, List<CM_Examine.SetAppraiseInfo>> appraisalObjects = new Dictionary<string, List<CM_Examine.SetAppraiseInfo>>();
                Dictionary<uint, string> appraisalObjectsCatagoryMap = new Dictionary<uint, string>();
                Dictionary<uint, string> weenieObjectsCatagoryMap = new Dictionary<uint, string>();
                Dictionary<uint, uint> appraisalObjectToWeenieId = new Dictionary<uint, uint>();

                Dictionary<uint, uint> staticObjectsWeenieType = new Dictionary<uint, uint>();
                Dictionary<uint, uint> weeniesWeenieType = new Dictionary<uint, uint>();

                List<uint> bookObjectIds = new List<uint>();
                Dictionary<string, Dictionary<uint, CM_Writing.PageDataList>> bookObjects = new Dictionary<string, Dictionary<uint, CM_Writing.PageDataList>>();
                List<uint> pageObjectIds = new List<uint>();
                Dictionary<string, Dictionary<uint, Dictionary<uint, CM_Writing.PageData>>> pageObjects = new Dictionary<string, Dictionary<uint, Dictionary<uint, CM_Writing.PageData>>>();

                List<uint> vendorObjectIds = new List<uint>();
                Dictionary<string, Dictionary<uint, CM_Vendor.gmVendorUI>> vendorObjects = new Dictionary<string, Dictionary<uint, CM_Vendor.gmVendorUI>>();
                Dictionary<uint, List<uint>> vendorSellsWeenies = new Dictionary<uint, List<uint>>();

                List<uint> slumlordObjectIds = new List<uint>();
                Dictionary<string, Dictionary<uint, CM_House.HouseProfile>> slumlordObjects = new Dictionary<string, Dictionary<uint, CM_House.HouseProfile>>();
                Dictionary<uint, List<uint>> slumlordBuyHouseWeenies = new Dictionary<uint, List<uint>>();
                Dictionary<uint, List<uint>> slumlordRentHouseWeenies = new Dictionary<uint, List<uint>>();

                Dictionary<uint, CM_Physics.CreateObject> weeniesTypeTemplate = new Dictionary<uint, CM_Physics.CreateObject>();
                Dictionary<uint, CM_Physics.CreateObject> weeniesFromVendors = new Dictionary<uint, CM_Physics.CreateObject>();

                while (true)
                {
                    if (searchAborted || Disposing || IsDisposed)
                        return;

                    KeyValuePair<string, List<FragDatListFile.FragDatInfo>> kvp;

                    if (!fragDatListFile.TryReadNext(out kvp))
                        break;

                    foreach (var frag in kvp.Value)
                    {
                        fragmentsProcessed++;

                        try
                        {
                            // ********************************************************************
                            // ********************** CUSTOM PROCESSING CODE ********************** 
                            // ********************************************************************
                            if (frag.Data.Length <= 4)
                                continue;

                            BinaryReader fragDataReader = new BinaryReader(new MemoryStream(frag.Data));

                            var messageCode = fragDataReader.ReadUInt32();

                            if (messageCode == (uint)PacketOpcode.Evt_Physics__CreateObject_ID)
                            {
                                var parsed = CM_Physics.CreateObject.read(fragDataReader);

                                CreateStaticObjectsList(parsed,
                                    objectIds, staticObjects,
                                    outputAsLandblocks, landblockInstances,
                                    weenieIds, weenies,
                                    processedWeeniePositions, dedupeWeenies,
                                    appraisalObjectsCatagoryMap, appraisalObjectToWeenieId,
                                    weenieObjectsCatagoryMap,
                                    weeniesWeenieType, staticObjectsWeenieType,
                                    wieldedObjectsParentMap, wieldedObjects,
                                    inventoryParents, inventoryObjects,
                                    parentWieldsWeenies,
                                    weeniesTypeTemplate,
                                    exportEverything);
                            }

                            if (messageCode == (uint)PacketOpcode.WEENIE_ORDERED_EVENT || messageCode == (uint)PacketOpcode.ORDERED_EVENT)
                            {
                                uint opCode = 0;

                                if (messageCode == (uint)PacketOpcode.WEENIE_ORDERED_EVENT)
                                {
                                    WOrderHdr orderHeader = WOrderHdr.read(fragDataReader);
                                    opCode = fragDataReader.ReadUInt32();
                                }
                                if (messageCode == (uint)PacketOpcode.ORDERED_EVENT)
                                {
                                    OrderHdr orderHeader = OrderHdr.read(fragDataReader);
                                    opCode = fragDataReader.ReadUInt32();
                                }

                                if (opCode == (uint)PacketOpcode.Evt_Physics__CreateObject_ID)
                                {
                                    var parsed = CM_Physics.CreateObject.read(fragDataReader);

                                    CreateStaticObjectsList(parsed,
                                        objectIds, staticObjects,
                                        outputAsLandblocks, landblockInstances,
                                        weenieIds, weenies,
                                        processedWeeniePositions, dedupeWeenies,
                                        appraisalObjectsCatagoryMap, appraisalObjectToWeenieId,
                                        weenieObjectsCatagoryMap,
                                        weeniesWeenieType, staticObjectsWeenieType,
                                        wieldedObjectsParentMap, wieldedObjects,
                                        inventoryParents, inventoryObjects,
                                        parentWieldsWeenies,
                                        weeniesTypeTemplate,
                                        exportEverything);
                                }

                                if (opCode == (uint)PacketOpcode.APPRAISAL_INFO_EVENT)
                                {
                                    var parsed = CM_Examine.SetAppraiseInfo.read(fragDataReader);

                                    CreateAppraisalObjectsList(parsed,
                                        objectIds, staticObjects,
                                        weenieIds, weenies,
                                        appraisalObjects, appraisalObjectIds, appraisalObjectsCatagoryMap, appraisalObjectToWeenieId);
                                }

                                if (opCode == (uint)PacketOpcode.BOOK_DATA_RESPONSE_EVENT)
                                {
                                    var parsed = CM_Writing.PageDataList.read(fragDataReader);

                                    CreateBookObjectsList(parsed,
                                        objectIds, staticObjects,
                                        weenieIds, weenies,
                                        appraisalObjects, appraisalObjectIds, appraisalObjectsCatagoryMap, appraisalObjectToWeenieId,
                                        bookObjectIds, bookObjects);
                                }

                                if (opCode == (uint)PacketOpcode.BOOK_PAGE_DATA_RESPONSE_EVENT)
                                {
                                    var parsed = CM_Writing.PageData.read(fragDataReader);

                                    CreatePageObjectsList(parsed,
                                        objectIds, staticObjects,
                                        weenieIds, weenies,
                                        appraisalObjects, appraisalObjectIds, appraisalObjectsCatagoryMap, appraisalObjectToWeenieId,
                                        pageObjectIds, pageObjects);
                                }

                                if (opCode == (uint)PacketOpcode.VENDOR_INFO_EVENT)
                                {
                                    var parsed = CM_Vendor.gmVendorUI.read(fragDataReader);

                                    CreateVendorObjectsList(parsed,
                                        objectIds, staticObjects,
                                        weenieIds, weenies,
                                        appraisalObjects, appraisalObjectIds, appraisalObjectsCatagoryMap, appraisalObjectToWeenieId,
                                        vendorObjectIds, vendorObjects, vendorSellsWeenies,
                                        weeniesFromVendors,
                                        weeniesTypeTemplate);
                                }

                                if (opCode == (uint)PacketOpcode.Evt_House__Recv_HouseProfile_ID)
                                {
                                    var parsed = CM_House.Recv_HouseProfile.read(fragDataReader);

                                    CreateSlumlordObjectsList(parsed,
                                        objectIds, staticObjects,
                                        weenieIds, weenies,
                                        appraisalObjects, appraisalObjectIds, appraisalObjectsCatagoryMap, appraisalObjectToWeenieId,
                                        slumlordObjectIds, slumlordObjects,
                                        weeniesTypeTemplate);
                                }
                            }
                        }
                        catch (EndOfStreamException) // This can happen when a frag is incomplete and we try to parse it
                        {
                            totalExceptions++;
                        }
                    }
                }

                if ((outputAsLandblocks && useLandblockTable) || outputWeenieFiles)
                {
                    GenerateMissingWeeniesFromVendors(objectIds, staticObjects,
                                        outputAsLandblocks, landblockInstances,
                                        weenieIds, weenies,
                                        processedWeeniePositions,
                                        appraisalObjectsCatagoryMap, appraisalObjectToWeenieId,
                                        weenieObjectsCatagoryMap,
                                        weeniesWeenieType, staticObjectsWeenieType,
                                        wieldedObjectsParentMap, wieldedObjects,
                                        inventoryParents, inventoryObjects,
                                        parentWieldsWeenies,
                                        weeniesTypeTemplate, weeniesFromVendors);
                }

                if (outputWeenieFiles)
                {
                    // WriteWeenieData(weenies, txtOutputFolder.Text, weeniesWeenieType);
                    WriteWeenieFiles(weenies, txtOutputFolder.Text, weeniesWeenieType);
                    // WriteWeenieAppraisalObjectData(appraisalObjects, txtOutputFolder.Text);
                    WriteAppendedWeenieAppraisalObjectData(appraisalObjects, txtOutputFolder.Text);
                    // WriteWeenieBookObjectData(bookObjects, txtOutputFolder.Text);

                    WriteAppendedCalculatedBurdenValueToWeenies(weenies, txtOutputFolder.Text, weeniesWeenieType);

                    WriteAppendedWeenieBookObjectData(bookObjects, txtOutputFolder.Text);
                    // WriteWeeniePageObjectData(pageObjects, txtOutputFolder.Text);
                    WriteAppendedWeeniePageObjectData(pageObjects, txtOutputFolder.Text);
                    // WriteWeenieVendorObjectData(vendorObjects, txtOutputFolder.Text);
                    WriteAppendedWeenieVendorObjectData(vendorObjects, txtOutputFolder.Text);
                    // WriteVendorInventory(vendorSellsWeenies, weenieIds, txtOutputFolder.Text);
                    WriteAppendedVendorInventory(vendorSellsWeenies, weenieIds, weenieObjectsCatagoryMap, txtOutputFolder.Text);
                    // WriteParentInventory(parentWieldsWeenies, weenieIds, txtOutputFolder.Text);
                    WriteAppendedParentInventory(parentWieldsWeenies, weenieIds, weenieObjectsCatagoryMap, txtOutputFolder.Text);

                    WriteAppendedSlumlordInventory(slumlordObjects, weenieIds, weenieObjectsCatagoryMap, txtOutputFolder.Text);

                    if (outputAsLandblocks)
                    {
                        if (useLandblockTable && !exportEverything)
                        {
                            WriteLandblockTable(landblockInstances, objectIds, txtOutputFolder.Text, staticObjectsWeenieType);

                            // WriteParentInventory(parentWieldsWeenies, weenieIds, txtOutputFolder.Text);

                            // WriteVendorInventory(vendorSellsWeenies, weenieIds, txtOutputFolder.Text);
                        }
                        //else
                        //{
                        //    WriteLandblockData(landblockInstances, objectIds, txtOutputFolder.Text, staticObjectsWeenieType, generateNewGuids);

                        //    if (!generateNewGuids)
                        //    {
                        //        WriteAppraisalObjectData(appraisalObjects, txtOutputFolder.Text);

                        //        WriteBookObjectData(bookObjects, txtOutputFolder.Text);

                        //        WritePageObjectData(pageObjects, txtOutputFolder.Text);

                        //        WriteVendorObjectData(vendorObjects, txtOutputFolder.Text);
                        //    }
                    }
                }
                else
                {
                    WriteWeenieData(weenies, txtOutputFolder.Text, weeniesWeenieType);

                    WriteWeenieAppraisalObjectData(appraisalObjects, txtOutputFolder.Text);

                    WriteWeenieBookObjectData(bookObjects, txtOutputFolder.Text);

                    WriteWeeniePageObjectData(pageObjects, txtOutputFolder.Text);

                    WriteWeenieVendorObjectData(vendorObjects, txtOutputFolder.Text);

                    if (outputAsLandblocks)
                    {
                        if (useLandblockTable && !exportEverything)
                        {
                            WriteLandblockTable(landblockInstances, objectIds, txtOutputFolder.Text, staticObjectsWeenieType);

                            WriteParentInventory(parentWieldsWeenies, weenieIds, txtOutputFolder.Text);

                            WriteVendorInventory(vendorSellsWeenies, weenieIds, txtOutputFolder.Text);
                        }
                        else
                        {
                            WriteLandblockData(landblockInstances, objectIds, txtOutputFolder.Text, staticObjectsWeenieType, generateNewGuids);

                            if (!generateNewGuids)
                            {
                                WriteAppraisalObjectData(appraisalObjects, txtOutputFolder.Text);

                                WriteBookObjectData(bookObjects, txtOutputFolder.Text);

                                WritePageObjectData(pageObjects, txtOutputFolder.Text);

                                WriteVendorObjectData(vendorObjects, txtOutputFolder.Text);
                            }
                        }
                    }
                    else
                    {
                        WriteStaticObjectData(staticObjects, objectIds, txtOutputFolder.Text, staticObjectsWeenieType);

                        WriteAppraisalObjectData(appraisalObjects, txtOutputFolder.Text);

                        //// WriteGeneratorObjectData(staticObjects, objectIds, txtOutputFolder.Text);

                        WriteBookObjectData(bookObjects, txtOutputFolder.Text);

                        WritePageObjectData(pageObjects, txtOutputFolder.Text);

                        WriteVendorObjectData(vendorObjects, txtOutputFolder.Text);
                    }
                }
                MessageBox.Show($"Export completed at {DateTime.Now.ToString()} and took {(DateTime.Now - start).TotalMinutes} minutes.");
            }
            finally
            {
                //foreach (var streamWriter in itemTypeStreamWriters.Values)
                //    streamWriter.Close();

                fragDatListFile.CloseFile();

                Interlocked.Increment(ref filesProcessed);
            }

            // Read in the temp file and save it to a new file with the column headers
            foreach (var kvp in itemTypeKeys)
            {
                if (kvp.Value.Count > 0)
                {
                    using (var writer = new StreamWriter(Path.Combine(txtOutputFolder.Text, kvp.Key + ".csv")))
                    {
                        var sb = new StringBuilder();

                        for (int i = 0; i < kvp.Value.Count; i++)
                        {
                            if (i > 0)
                                sb.Append(',');

                            sb.Append(kvp.Value[i] ?? String.Empty);
                        }

                        writer.WriteLine(sb.ToString());

                        using (var reader = new StreamReader(Path.Combine(txtOutputFolder.Text, kvp.Key + ".csv.temp")))
                        {
                            string line;
                            while ((line = reader.ReadLine()) != null)
                                writer.WriteLine(line);
                        }
                    }
                }

                File.Delete(Path.Combine(txtOutputFolder.Text, kvp.Key + ".csv.temp"));
            }
        }

        private void WriteAppendedCalculatedBurdenValueToWeenies(Dictionary<string, List<CM_Physics.CreateObject>> weenies, string outputFolder, Dictionary<uint, uint> weeniesWeenieType)
        {
            string templateFolder = Path.Combine(outputFolder, "1-weenies");

            //string sqlCommand = "INSERT";
            string sqlCommand = "REPLACE";

            if (!Directory.Exists(templateFolder))
                Directory.CreateDirectory(templateFolder);

            foreach (string key in weenies.Keys)
            {
                //string filename = Path.Combine(templateFolder, $"{key}.sql");

                //if (File.Exists(filename))
                //    File.Delete(filename);

                foreach (var parsed in weenies[key])
                {
                    //bool once = false;

                    //if (File.Exists(fullFile))
                    //{
                    //    FileInfo fi = new FileInfo(fullFile);

                    //    // go to the next file if it's bigger than a MB
                    //    if (fi.Length > ((1048576) * 40))
                    //    {
                    //        fileCount[parsed.wdesc._type]++;
                    //        fullFile = Path.Combine(staticFolder, $"{parsed.wdesc._type}_{fileCount[parsed.wdesc._type]}.sql");
                    //    }
                    //}

                    string keyFolder = Path.Combine(templateFolder, key);

                    //if (!Directory.Exists(keyFolder))
                    //    Directory.CreateDirectory(keyFolder);

                    string filename = Path.Combine(keyFolder, $"{parsed.wdesc._wcid}.sql");

                    if (key == "CreaturesUnsorted")
                    {
                        // do magical name sorting here.. 
                    }

                    //if (File.Exists(filename))
                    //    File.Delete(filename);

                    if (!((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_StackSize) != 0))
                    {
                        continue;
                    }

                    using (FileStream fs = new FileStream(filename, FileMode.Append))
                    {
                        using (StreamWriter writer = new StreamWriter(fs))
                        {
                            string line = $"/* Calculated Burden/Value and Adjusted StackSize Data */" + Environment.NewLine;
                            writer.WriteLine(line);

                            string intsLine = "", bigintsLine = "", floatsLine = "", boolsLine = "", strsLine = "", didsLine = "", iidsLine = "";

                            ushort calcBurden = 0, stackSize = 1;
                            uint calcValue = 0;

                            calcBurden = (ushort)(parsed.wdesc._burden / parsed.wdesc._stackSize);

                            calcValue = parsed.wdesc._value / parsed.wdesc._stackSize;

                            if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_Burden) != 0)
                                intsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeInt.ENCUMB_VAL_INT}, {calcBurden}) /* {Enum.GetName(typeof(STypeInt), STypeInt.ENCUMB_VAL_INT)} */" + Environment.NewLine;
                            if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_MaxStackSize) != 0)
                                intsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeInt.MAX_STACK_SIZE_INT}, {parsed.wdesc._maxStackSize}) /* {Enum.GetName(typeof(STypeInt), STypeInt.MAX_STACK_SIZE_INT)} */" + Environment.NewLine;
                            //if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_MaxStructure) != 0)
                            //    intsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeInt.MAX_STRUCTURE_INT}, {parsed.wdesc._maxStructure}) /* {Enum.GetName(typeof(STypeInt), STypeInt.MAX_STRUCTURE_INT)} */" + Environment.NewLine;
                            //if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_StackSize) != 0)
                            //    intsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeInt.STACK_SIZE_INT}, {parsed.wdesc._stackSize})" + Environment.NewLine;
                            if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_StackSize) != 0)
                                intsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeInt.STACK_SIZE_INT}, {stackSize}) /* {Enum.GetName(typeof(STypeInt), STypeInt.STACK_SIZE_INT)} */" + Environment.NewLine;
                            //if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_Structure) != 0)
                            //    intsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeInt.STRUCTURE_INT}, {parsed.wdesc._structure})" + Environment.NewLine;
                            //if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_Structure) != 0)
                            //    intsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeInt.STRUCTURE_INT}, {parsed.wdesc._maxStructure}) /* {Enum.GetName(typeof(STypeInt), STypeInt.STRUCTURE_INT)} */" + Environment.NewLine;
                            if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_Value) != 0)
                                intsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeInt.VALUE_INT}, {calcValue}) /* {Enum.GetName(typeof(STypeInt), STypeInt.VALUE_INT)} */" + Environment.NewLine;

                            if (strsLine != "")
                            {
                                strsLine = $"{sqlCommand} INTO `ace_object_properties_string` (`aceObjectId`, `strPropertyId`, `propertyValue`)" + Environment.NewLine
                                    + "VALUES " + strsLine.TrimStart("     ,".ToCharArray());
                                strsLine = strsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                writer.WriteLine(strsLine);
                            }
                            if (didsLine != "")
                            {
                                didsLine = $"{sqlCommand} INTO `ace_object_properties_did` (`aceObjectId`, `didPropertyId`, `propertyValue`)" + Environment.NewLine
                                    + "VALUES " + didsLine.TrimStart("     ,".ToCharArray());
                                didsLine = didsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                writer.WriteLine(didsLine);
                            }
                            if (iidsLine != "")
                            {
                                iidsLine = $"{sqlCommand} INTO `ace_object_properties_iid` (`aceObjectId`, `iidPropertyId`, `propertyValue`)" + Environment.NewLine
                                    + "VALUES " + iidsLine.TrimStart("     ,".ToCharArray());
                                iidsLine = iidsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                writer.WriteLine(iidsLine);
                            }
                            if (intsLine != "")
                            {
                                intsLine = $"{sqlCommand} INTO `ace_object_properties_int` (`aceObjectId`, `intPropertyId`, `propertyValue`)" + Environment.NewLine
                                    + "VALUES " + intsLine.TrimStart("     ,".ToCharArray());
                                intsLine = intsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                writer.WriteLine(intsLine);
                            }
                            if (bigintsLine != "")
                            {
                                bigintsLine = $"{sqlCommand} INTO `ace_object_properties_bigint` (`aceObjectId`, `bigIntPropertyId`, `propertyValue`)" + Environment.NewLine
                                    + "VALUES " + bigintsLine.TrimStart("     ,".ToCharArray());
                                bigintsLine = bigintsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                writer.WriteLine(bigintsLine);
                            }
                            if (floatsLine != "")
                            {
                                floatsLine = $"{sqlCommand} INTO `ace_object_properties_double` (`aceObjectId`, `dblPropertyId`, `propertyValue`)" + Environment.NewLine
                                    + "VALUES " + floatsLine.TrimStart("     ,".ToCharArray());
                                floatsLine = floatsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                writer.WriteLine(floatsLine);
                            }
                            if (boolsLine != "")
                            {
                                boolsLine = $"{sqlCommand} INTO `ace_object_properties_bool` (`aceObjectId`, `boolPropertyId`, `propertyValue`)" + Environment.NewLine
                                    + "VALUES " + boolsLine.TrimStart("     ,".ToCharArray());
                                boolsLine = boolsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                writer.WriteLine(boolsLine);
                            }
                        }
                    }
                }
            }
        }

        private void WriteAppendedSlumlordInventory(Dictionary<string, Dictionary<uint, CM_House.HouseProfile>> slumlordObjects, List<uint> weenieIds, Dictionary<uint, string> appraisalObjectsCatagoryMap, string outputFolder)
        {
            string staticFolder = Path.Combine(outputFolder, "1-weenies");

            //string sqlCommand = "INSERT";
            string sqlCommand = "REPLACE";

            //if (!Directory.Exists(staticFolder))
            //    Directory.CreateDirectory(staticFolder);

            //Dictionary<string, int> fileCount = new Dictionary<string, int>();

            foreach (var key in slumlordObjects.Keys)
            {
                foreach (var slumlord in slumlordObjects[key])
                {
                    try
                    {
                        // string key = "";
                        // appraisalObjectsCatagoryMap.TryGetValue(vendor, out key);

                        string keyFolder = Path.Combine(staticFolder, key);

                        string fullFile = Path.Combine(keyFolder, $"{slumlord.Key}.sql");

                        //string fullFile = Path.Combine(staticFolder, $"{vendor}.sql");

                        //if (File.Exists(fullFile))
                        //{
                        //    FileInfo fi = new FileInfo(fullFile);

                        //    // go to the next file if it's bigger than a MB
                        //    if (fi.Length > ((1048576) * 40))
                        //    {

                        //        if (File.Exists(fullFile))
                        //            File.Delete(fullFile);
                        //    }
                        //}

                        using (FileStream fs = new FileStream(fullFile, FileMode.Append))
                        {
                            using (StreamWriter writer = new StreamWriter(fs))
                            {
                                string header = $"/* Slumlord Extended Properties */" + Environment.NewLine;
                                writer.WriteLine(header);

                                string instanceLine = "", didsLine = "",  intsLine = "", boolsLine = "";

                                //didsLine += $"     , ({slumlord.Key}, {(uint)STypeDID.HOUSEID_DID}, {(uint)slumlord.Value._id}) /* {Enum.GetName(typeof(STypeDID), STypeDID.HOUSEID_DID)} */" + Environment.NewLine;

                                intsLine += $"     , ({slumlord.Key}, {(uint)STypeInt.HOUSE_TYPE_INT}, {(uint)slumlord.Value._type}) /* {Enum.GetName(typeof(STypeInt), STypeInt.HOUSE_TYPE_INT)} */" + Environment.NewLine;
                                intsLine += $"     , ({slumlord.Key}, {(uint)STypeInt.HOUSE_STATUS_INT}, {(uint)slumlord.Value._bitmask}) /* {Enum.GetName(typeof(STypeInt), STypeInt.HOUSE_STATUS_INT)} */" + Environment.NewLine;
                                if (slumlord.Value._min_level > -1)
                                    intsLine += $"     , ({slumlord.Key}, {(uint)STypeInt.MIN_LEVEL_INT}, {(uint)slumlord.Value._min_level}) /* {Enum.GetName(typeof(STypeInt), STypeInt.MIN_LEVEL_INT)} */" + Environment.NewLine;
                                if (slumlord.Value._max_level > -1)
                                    intsLine += $"     , ({slumlord.Key}, {(uint)STypeInt.MAX_LEVEL_INT}, {(uint)slumlord.Value._max_level}) /* {Enum.GetName(typeof(STypeInt), STypeInt.MAX_LEVEL_INT)} */" + Environment.NewLine;
                                if (slumlord.Value._min_alleg_rank > -1)
                                    intsLine += $"     , ({slumlord.Key}, {(uint)STypeInt.ALLEGIANCE_MIN_LEVEL_INT}, {(uint)slumlord.Value._min_alleg_rank}) /* {Enum.GetName(typeof(STypeInt), STypeInt.ALLEGIANCE_MIN_LEVEL_INT)} */" + Environment.NewLine;
                                if (slumlord.Value._max_alleg_rank > -1)
                                    intsLine += $"     , ({slumlord.Key}, {(uint)STypeInt.ALLEGIANCE_MAX_LEVEL_INT}, {(uint)slumlord.Value._max_alleg_rank}) /* {Enum.GetName(typeof(STypeInt), STypeInt.ALLEGIANCE_MAX_LEVEL_INT)} */" + Environment.NewLine;

                                if (slumlord.Value._maintenance_free == 0)
                                    boolsLine += $"     , ({slumlord.Key}, {(uint)STypeBool.ROT_PROOF_BOOL}, {false}) /* {Enum.GetName(typeof(STypeBool), STypeBool.ROT_PROOF_BOOL)} */" + Environment.NewLine;
                                else
                                    boolsLine += $"     , ({slumlord.Key}, {(uint)STypeBool.ROT_PROOF_BOOL}, {true}) /* {Enum.GetName(typeof(STypeBool), STypeBool.ROT_PROOF_BOOL)} */" + Environment.NewLine;

                                foreach (var item in slumlord.Value._buy.list)
                                {
                                    if (weenieIds.Contains(item.wcid))
                                    {
                                        instanceLine += $"     , ({slumlord.Key}, {(uint)DestinationType.HouseBuy_DestinationType}, {item.wcid}, {item.num}" +
                                        //$"{parsed.physicsdesc.pos.objcell_id}, " +
                                        //$"{parsed.physicsdesc.pos.frame.m_fOrigin.x}, {parsed.physicsdesc.pos.frame.m_fOrigin.y}, {parsed.physicsdesc.pos.frame.m_fOrigin.z}, " +
                                        //$"{parsed.physicsdesc.pos.frame.qw}, {parsed.physicsdesc.pos.frame.qx}, {parsed.physicsdesc.pos.frame.qy}, {parsed.physicsdesc.pos.frame.qz}" +
                                        $") /* {item.name} */" + Environment.NewLine;
                                    }
                                    else
                                    {
                                        System.Diagnostics.Debug.WriteLine($"Slumlord buy list requests ({item.name} - {item.wcid}) and is not in the the weenie list.");
                                        totalExceptions++;
                                    }
                                }

                                foreach (var item in slumlord.Value._rent.list)
                                {
                                    if (weenieIds.Contains(item.wcid))
                                    {
                                        instanceLine += $"     , ({slumlord.Key}, {(uint)DestinationType.HouseRent_DestinationType}, {item.wcid}, {item.num}" +
                                        //$"{parsed.physicsdesc.pos.objcell_id}, " +
                                        //$"{parsed.physicsdesc.pos.frame.m_fOrigin.x}, {parsed.physicsdesc.pos.frame.m_fOrigin.y}, {parsed.physicsdesc.pos.frame.m_fOrigin.z}, " +
                                        //$"{parsed.physicsdesc.pos.frame.qw}, {parsed.physicsdesc.pos.frame.qx}, {parsed.physicsdesc.pos.frame.qy}, {parsed.physicsdesc.pos.frame.qz}" +
                                        $") /* {item.name} */" + Environment.NewLine;
                                    }
                                    else
                                    {
                                        System.Diagnostics.Debug.WriteLine($"Slumlord rent list requests ({item.name} - {item.wcid}) and is not in the the weenie list.");
                                        totalExceptions++;
                                    }
                                }

                                if (didsLine != "")
                                {
                                    didsLine = $"{sqlCommand} INTO `ace_object_properties_did` (`aceObjectId`, `didPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + didsLine.TrimStart("     ,".ToCharArray());
                                    didsLine = didsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(didsLine);
                                }

                                if (intsLine != "")
                                {
                                    intsLine = $"{sqlCommand} INTO `ace_object_properties_int` (`aceObjectId`, `intPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + intsLine.TrimStart("     ,".ToCharArray());
                                    intsLine = intsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(intsLine);
                                }

                                if (boolsLine != "")
                                {
                                    boolsLine = $"{sqlCommand} INTO `ace_object_properties_bool` (`aceObjectId`, `boolPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + boolsLine.TrimStart("     ,".ToCharArray());
                                    boolsLine = boolsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(boolsLine);
                                }

                                if (instanceLine != "")
                                {
                                    instanceLine = $"{sqlCommand} INTO `ace_object_inventory` (`aceObjectId`, `destinationType`, `weenieClassId`, `stackSize`)" + Environment.NewLine
                                        + "VALUES " + instanceLine.TrimStart("     ,".ToCharArray());
                                    instanceLine = instanceLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(instanceLine);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("Unable to export slumlord " + slumlord + ". Exception:" + Environment.NewLine + ex.ToString());
                    }
                }
            }
        }

        private void GenerateMissingWeeniesFromVendors(List<uint> objectIds, Dictionary<string, List<CM_Physics.CreateObject>> staticObjects,
            bool saveAsLandblockInstances, Dictionary<uint, Dictionary<string, List<CM_Physics.CreateObject>>> landblockInstances,
            List<uint> weenieIds, Dictionary<string, List<CM_Physics.CreateObject>> weenies, Dictionary<uint, List<Position>> processedWeeniePositions,
            Dictionary<uint, string> appraisalObjectsCatagoryMap, Dictionary<uint, uint> appraisalObjectToWeenieId,
            Dictionary<uint, string> weenieObjectsCatagoryMap, Dictionary<uint, uint> weeniesWeenieType,
            Dictionary<uint, uint> staticObjectsWeenieType, Dictionary<uint, List<uint>> wieldedObjectsParentMap, Dictionary<uint, List<CM_Physics.CreateObject>> wieldedObjects,
            Dictionary<uint, List<uint>> inventoryParents, Dictionary<uint, CM_Physics.CreateObject> inventoryObjects,
            Dictionary<uint, List<uint>> parentWieldsWeenies,
            Dictionary<uint, CM_Physics.CreateObject> weeniesTypeTemplate,
            Dictionary<uint, CM_Physics.CreateObject> weeniesFromVendors)
        {
            try
            {
                foreach (var weenie in weeniesFromVendors)
                {
                    if (weenieIds.Contains(weenie.Key))
                        continue;

                    var parsed = weenie.Value;

                    CreateStaticObjectsList(parsed,
                        objectIds, staticObjects,
                        saveAsLandblockInstances, landblockInstances,
                        weenieIds, weenies,
                        processedWeeniePositions, true,
                        appraisalObjectsCatagoryMap, appraisalObjectToWeenieId,
                        weenieObjectsCatagoryMap,
                        weeniesWeenieType, staticObjectsWeenieType,
                        wieldedObjectsParentMap, wieldedObjects,
                        inventoryParents, inventoryObjects,
                        parentWieldsWeenies,
                        weeniesTypeTemplate,
                        false);
                }
            }
            catch (Exception ex)
            {
                // MessageBox.Show(ex.ToString());
                totalExceptions++;
            }
        }

        private void WriteAppendedParentInventory(Dictionary<uint, List<uint>> parentWieldsWeenies, List<uint> weenieIds, Dictionary<uint, string> appraisalObjectsCatagoryMap, string outputFolder)
        {
            string staticFolder = Path.Combine(outputFolder, "1-weenies");

            //string sqlCommand = "INSERT";
            string sqlCommand = "REPLACE";

            //if (!Directory.Exists(staticFolder))
            //    Directory.CreateDirectory(staticFolder);

            //Dictionary<string, int> fileCount = new Dictionary<string, int>();

            foreach (var parent in parentWieldsWeenies.Keys)
            {
                try
                {
                    string key = "";
                    appraisalObjectsCatagoryMap.TryGetValue(parent, out key);

                    string keyFolder = Path.Combine(staticFolder, key);

                    string fullFile = Path.Combine(keyFolder, $"{parent}.sql");
                    //string fullFile = Path.Combine(staticFolder, $"{parent}.sql");

                    //if (File.Exists(fullFile))
                    //{
                    //    FileInfo fi = new FileInfo(fullFile);

                    //    // go to the next file if it's bigger than a MB
                    //    if (fi.Length > ((1048576) * 40))
                    //    {

                    //        if (File.Exists(fullFile))
                    //            File.Delete(fullFile);
                    //    }
                    //}

                    using (FileStream fs = new FileStream(fullFile, FileMode.Append))
                    {
                        using (StreamWriter writer = new StreamWriter(fs))
                        {
                            string instanceLine = "";

                            foreach (var item in parentWieldsWeenies[parent])
                            {
                                string header = $"/* Object Wield List */" + Environment.NewLine;
                                writer.WriteLine(header);

                                if (weenieIds.Contains(item))
                                {
                                    instanceLine += $"     , ({parent}, {(uint)DestinationType.Wield_DestinationType}, {item}" +
                                    //$"{parsed.physicsdesc.pos.objcell_id}, " +
                                    //$"{parsed.physicsdesc.pos.frame.m_fOrigin.x}, {parsed.physicsdesc.pos.frame.m_fOrigin.y}, {parsed.physicsdesc.pos.frame.m_fOrigin.z}, " +
                                    //$"{parsed.physicsdesc.pos.frame.qw}, {parsed.physicsdesc.pos.frame.qx}, {parsed.physicsdesc.pos.frame.qy}, {parsed.physicsdesc.pos.frame.qz}" +
                                    ")" + Environment.NewLine;
                                }
                                else
                                {
                                    System.Diagnostics.Debug.WriteLine($"{parent} wields ({item}) and is not in the the weenie list.");
                                    totalExceptions++;
                                }
                            }

                            if (instanceLine != "")
                            {
                                instanceLine = $"{sqlCommand} INTO `ace_object_inventory` (`aceObjectId`, `destinationType`, `weenieClassId`)" + Environment.NewLine
                                    + "VALUES " + instanceLine.TrimStart("     ,".ToCharArray());
                                instanceLine = instanceLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                writer.WriteLine(instanceLine);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Unable to export parent " + parent + ". Exception:" + Environment.NewLine + ex.ToString());
                }
            }
        }

        private void WriteParentInventory(Dictionary<uint, List<uint>> parentWieldsWeenies, List<uint> weenieIds, string outputFolder)
        {
            string staticFolder = Path.Combine(outputFolder, "7-parentdata");

            //string sqlCommand = "INSERT";
            string sqlCommand = "REPLACE";

            if (!Directory.Exists(staticFolder))
                Directory.CreateDirectory(staticFolder);

            Dictionary<string, int> fileCount = new Dictionary<string, int>();

            foreach (var parent in parentWieldsWeenies.Keys)
            {
                try
                {
                    string fullFile = Path.Combine(staticFolder, $"{parent}.sql");

                    if (File.Exists(fullFile))
                    {
                        FileInfo fi = new FileInfo(fullFile);

                        // go to the next file if it's bigger than a MB
                        if (fi.Length > ((1048576) * 40))
                        {

                            if (File.Exists(fullFile))
                                File.Delete(fullFile);
                        }
                    }

                    using (FileStream fs = new FileStream(fullFile, FileMode.Append))
                    {
                        using (StreamWriter writer = new StreamWriter(fs))
                        {
                            string instanceLine = "";

                            foreach (var item in parentWieldsWeenies[parent])
                            {
                                if (weenieIds.Contains(item))
                                {
                                    instanceLine += $"     , ({parent}, {item}, {(uint)DestinationType.Wield_DestinationType}" +
                                    //$"{parsed.physicsdesc.pos.objcell_id}, " +
                                    //$"{parsed.physicsdesc.pos.frame.m_fOrigin.x}, {parsed.physicsdesc.pos.frame.m_fOrigin.y}, {parsed.physicsdesc.pos.frame.m_fOrigin.z}, " +
                                    //$"{parsed.physicsdesc.pos.frame.qw}, {parsed.physicsdesc.pos.frame.qx}, {parsed.physicsdesc.pos.frame.qy}, {parsed.physicsdesc.pos.frame.qz}" +
                                    ")" + Environment.NewLine;
                                }
                                else
                                {
                                    System.Diagnostics.Debug.WriteLine($"{parent} wields ({item}) and is not in the the weenie list.");
                                    totalExceptions++;
                                }
                            }

                            if (instanceLine != "")
                            {
                                instanceLine = $"{sqlCommand} INTO `ace_object_inventory` (`aceObjectId`, `weenieClassId`, `destinationType`)" + Environment.NewLine
                                    + "VALUES " + instanceLine.TrimStart("     ,".ToCharArray());
                                instanceLine = instanceLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                writer.WriteLine(instanceLine);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Unable to export parent " + parent + ". Exception:" + Environment.NewLine + ex.ToString());
                }
            }
        }

        private void WriteAppendedVendorInventory(Dictionary<uint, List<uint>> vendorSellsWeenies, List<uint> weenieIds, Dictionary<uint, string> appraisalObjectsCatagoryMap, string outputFolder)
        {
            string staticFolder = Path.Combine(outputFolder, "1-weenies");

            //string sqlCommand = "INSERT";
            string sqlCommand = "REPLACE";

            //if (!Directory.Exists(staticFolder))
            //    Directory.CreateDirectory(staticFolder);

            //Dictionary<string, int> fileCount = new Dictionary<string, int>();

            foreach (var vendor in vendorSellsWeenies.Keys)
            {
                try
                {
                    string key = "";
                    appraisalObjectsCatagoryMap.TryGetValue(vendor, out key);

                    string keyFolder = Path.Combine(staticFolder, key);

                    string fullFile = Path.Combine(keyFolder, $"{vendor}.sql");

                    //string fullFile = Path.Combine(staticFolder, $"{vendor}.sql");

                    //if (File.Exists(fullFile))
                    //{
                    //    FileInfo fi = new FileInfo(fullFile);

                    //    // go to the next file if it's bigger than a MB
                    //    if (fi.Length > ((1048576) * 40))
                    //    {

                    //        if (File.Exists(fullFile))
                    //            File.Delete(fullFile);
                    //    }
                    //}

                    using (FileStream fs = new FileStream(fullFile, FileMode.Append))
                    {
                        using (StreamWriter writer = new StreamWriter(fs))
                        {
                            string header = $"/* Vendor Shop Selection List */" + Environment.NewLine;
                            writer.WriteLine(header);

                            string instanceLine = "";

                            foreach (var item in vendorSellsWeenies[vendor])
                            {
                                if (weenieIds.Contains(item))
                                {
                                    instanceLine += $"     , ({vendor}, {(uint)DestinationType.Shop_DestinationType}, {item}" +
                                    //$"{parsed.physicsdesc.pos.objcell_id}, " +
                                    //$"{parsed.physicsdesc.pos.frame.m_fOrigin.x}, {parsed.physicsdesc.pos.frame.m_fOrigin.y}, {parsed.physicsdesc.pos.frame.m_fOrigin.z}, " +
                                    //$"{parsed.physicsdesc.pos.frame.qw}, {parsed.physicsdesc.pos.frame.qx}, {parsed.physicsdesc.pos.frame.qy}, {parsed.physicsdesc.pos.frame.qz}" +
                                    ")" + Environment.NewLine;
                                }
                                else
                                {
                                    System.Diagnostics.Debug.WriteLine($"Vendor sells ({item}) and is not in the the weenie list.");
                                    totalExceptions++;
                                }
                            }

                            if (instanceLine != "")
                            {
                                instanceLine = $"{sqlCommand} INTO `ace_object_inventory` (`aceObjectId`, `destinationType`, `weenieClassId`)" + Environment.NewLine
                                    + "VALUES " + instanceLine.TrimStart("     ,".ToCharArray());
                                instanceLine = instanceLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                writer.WriteLine(instanceLine);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Unable to export vendor " + vendor + ". Exception:" + Environment.NewLine + ex.ToString());
                }
            }
        }

        private void WriteVendorInventory(Dictionary<uint, List<uint>> vendorSellsWeenies, List<uint> weenieIds, string outputFolder)
        {
            string staticFolder = Path.Combine(outputFolder, "8-vendordata");

            //string sqlCommand = "INSERT";
            string sqlCommand = "REPLACE";

            if (!Directory.Exists(staticFolder))
                Directory.CreateDirectory(staticFolder);

            Dictionary<string, int> fileCount = new Dictionary<string, int>();

            foreach (var vendor in vendorSellsWeenies.Keys)
            {
                try
                {
                    string fullFile = Path.Combine(staticFolder, $"{vendor}.sql");

                    if (File.Exists(fullFile))
                        {
                            FileInfo fi = new FileInfo(fullFile);

                            // go to the next file if it's bigger than a MB
                            if (fi.Length > ((1048576) * 40))
                            {

                                if (File.Exists(fullFile))
                                    File.Delete(fullFile);
                            }
                        }

                        using (FileStream fs = new FileStream(fullFile, FileMode.Append))
                        {
                            using (StreamWriter writer = new StreamWriter(fs))
                            {
                            string instanceLine = "";

                            foreach (var item in vendorSellsWeenies[vendor])
                            {
                                if (weenieIds.Contains(item))
                                {
                                    instanceLine += $"     , ({vendor}, {item}, {(uint)DestinationType.Shop_DestinationType}" +
                                    //$"{parsed.physicsdesc.pos.objcell_id}, " +
                                    //$"{parsed.physicsdesc.pos.frame.m_fOrigin.x}, {parsed.physicsdesc.pos.frame.m_fOrigin.y}, {parsed.physicsdesc.pos.frame.m_fOrigin.z}, " +
                                    //$"{parsed.physicsdesc.pos.frame.qw}, {parsed.physicsdesc.pos.frame.qx}, {parsed.physicsdesc.pos.frame.qy}, {parsed.physicsdesc.pos.frame.qz}" +
                                    ")" + Environment.NewLine;
                                }
                                else
                                {
                                    System.Diagnostics.Debug.WriteLine($"Vendor sells ({item}) and is not in the the weenie list.");
                                    totalExceptions++;
                                }
                            }

                            if (instanceLine != "")
                            {
                                instanceLine = $"{sqlCommand} INTO `ace_object_inventory` (`aceObjectId`, `weenieClassId`, `destinationType`)" + Environment.NewLine
                                    + "VALUES " + instanceLine.TrimStart("     ,".ToCharArray());
                                instanceLine = instanceLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                writer.WriteLine(instanceLine);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Unable to export vendor " + vendor + ". Exception:" + Environment.NewLine + ex.ToString());
                }
            }
        }

        private void CreateSlumlordObjectsList(CM_House.Recv_HouseProfile parsed, List<uint> objectIds, Dictionary<string, List<CM_Physics.CreateObject>> staticObjects,
                    List<uint> weenieIds, Dictionary<string, List<CM_Physics.CreateObject>> weenies, Dictionary<string, List<CM_Examine.SetAppraiseInfo>> appraisalObjects,
                    List<uint> appraisalObjectIds, Dictionary<uint, string> appraisalObjectsCatagoryMap, Dictionary<uint, uint> appraisalObjectToWeenieId,
                    List<uint> slumlordObjectIds, Dictionary<string, Dictionary<uint, CM_House.HouseProfile>> slumlordObjects, 
                    Dictionary<uint, CM_Physics.CreateObject> weeniesTypeTemplate)
        {
            try
            {
                uint weenieId = 0;
                bool foundInObjectIds = false;
                bool foundInWeenieIds = false;
                foundInObjectIds = objectIds.Contains(parsed.lord);
                appraisalObjectToWeenieId.TryGetValue(parsed.lord, out weenieId);
                foundInWeenieIds = weenieIds.Contains(weenieId);

                if (!foundInObjectIds && !(weenieId > 0))
                    return;

                bool addIt = true;
                //bool addWeenie = false;
                string fileToPutItIn = "HouseData";


                appraisalObjectsCatagoryMap.TryGetValue(parsed.lord, out fileToPutItIn);

                if (fileToPutItIn == null)
                    fileToPutItIn = "0-HouseData";

                if (!foundInObjectIds && weenieId > 0)
                {
                    if (!foundInWeenieIds)
                        return;

                    parsed.lord = weenieId;
                }

                if (slumlordObjectIds.Contains(weenieId))
                    return;

                parsed.lord = weenieId;

                // de-dupe based on position and wcid
                if (addIt) //&& !PositionRecorded(parsed, processedWeeniePositions[parsed.wdesc._wcid], parsed.physicsdesc.pos, margin))
                {
                    if (!slumlordObjects.ContainsKey(fileToPutItIn))
                    {
                        slumlordObjects.Add(fileToPutItIn, new Dictionary<uint, CM_House.HouseProfile>());
                    }

                    if (slumlordObjectIds.Contains(parsed.lord))
                    {
                        return;
                    }

                    slumlordObjects[fileToPutItIn].Add(parsed.lord, parsed.prof);
                    slumlordObjectIds.Add(parsed.lord);

                    //foreach (var item in parsed.shopItemProfileList.list)
                    //{
                    //    //if (!vendorSellsWeenies.ContainsKey(parsed.shopVendorID))
                    //    //    vendorSellsWeenies.Add(parsed.shopVendorID, new List<uint>());

                    //    //vendorSellsWeenies[parsed.shopVendorID].Add(item.pwd._wcid);

                    //    if (!vendorSellsWeenies.ContainsKey(weenieId))
                    //        vendorSellsWeenies.Add(weenieId, new List<uint>());

                    //    if (!vendorSellsWeenies[weenieId].Contains(item.pwd._wcid))
                    //        vendorSellsWeenies[weenieId].Add(item.pwd._wcid);

                    //    if (!weeniesFromVendors.ContainsKey(item.pwd._wcid))
                    //    {
                    //        CreateObject newObj = GenerateCreateObjectfromVendorItemProfile(item, weeniesTypeTemplate);

                    //        if (newObj == null)
                    //            continue;

                    //        weeniesFromVendors.Add(item.pwd._wcid, newObj);
                    //    }
                    //}

                    //if (!vendorObjectIds.Contains(weenieId) && weenieId > 0)
                    //{
                    //    CM_Vendor.gmVendorUI parsedClone;

                    //    parsedClone = new CM_Vendor.gmVendorUI();
                    //    parsedClone.shopVendorID = weenieId;

                    //    parsedClone.shopVendorProfile = parsed.shopVendorProfile;
                    //    parsedClone.shopItemProfileList = parsed.shopItemProfileList;

                    //    if (!vendorObjects.ContainsKey(fileToPutItIn))
                    //    {
                    //        vendorObjects.Add(fileToPutItIn, new Dictionary<uint, CM_Vendor.gmVendorUI>());
                    //    }

                    //    vendorObjects[fileToPutItIn].Add(parsedClone.shopVendorID, parsedClone);
                    //    vendorObjectIds.Add(parsedClone.shopVendorID);
                    //}
                    totalHits++;
                }
            }
            catch (Exception ex)
            {
                totalExceptions++;
            }
        }

        private void CreateVendorObjectsList(CM_Vendor.gmVendorUI parsed, List<uint> objectIds, Dictionary<string, List<CM_Physics.CreateObject>> staticObjects,
            List<uint> weenieIds, Dictionary<string, List<CM_Physics.CreateObject>> weenies, Dictionary<string, List<CM_Examine.SetAppraiseInfo>> appraisalObjects,
            List<uint> appraisalObjectIds, Dictionary<uint, string> appraisalObjectsCatagoryMap, Dictionary<uint, uint> appraisalObjectToWeenieId,
            List<uint> vendorObjectIds, Dictionary<string, Dictionary<uint, CM_Vendor.gmVendorUI>> vendorObjects, Dictionary<uint, List<uint>> vendorSellsWeenies,
            Dictionary<uint, CM_Physics.CreateObject> weeniesFromVendors,
            Dictionary<uint, CM_Physics.CreateObject> weeniesTypeTemplate)
        {
            try
            {
                uint weenieId = 0;
                bool foundInObjectIds = false;
                bool foundInWeenieIds = false;
                foundInObjectIds = objectIds.Contains(parsed.shopVendorID);
                appraisalObjectToWeenieId.TryGetValue(parsed.shopVendorID, out weenieId);
                foundInWeenieIds = weenieIds.Contains(weenieId);

                if (!foundInObjectIds && !(weenieId > 0))
                    return;

                bool addIt = true;
                //bool addWeenie = false;
                string fileToPutItIn = "VendorData";


                appraisalObjectsCatagoryMap.TryGetValue(parsed.shopVendorID, out fileToPutItIn);

                if (fileToPutItIn == null)
                    fileToPutItIn = "0-VendorData";

                if (!foundInObjectIds && weenieId > 0)
                {
                    if (!foundInWeenieIds)
                        return;

                    parsed.shopVendorID = weenieId;
                }

                // de-dupe based on position and wcid
                if (addIt) //&& !PositionRecorded(parsed, processedWeeniePositions[parsed.wdesc._wcid], parsed.physicsdesc.pos, margin))
                {
                    if (!vendorObjects.ContainsKey(fileToPutItIn))
                    {
                        vendorObjects.Add(fileToPutItIn, new Dictionary<uint, CM_Vendor.gmVendorUI>());
                    }

                    if (vendorObjectIds.Contains(parsed.shopVendorID))
                    {
                        return;
                    }

                    vendorObjects[fileToPutItIn].Add(parsed.shopVendorID, parsed);
                    vendorObjectIds.Add(parsed.shopVendorID);

                    foreach (var item in parsed.shopItemProfileList.list)
                    {
                        //if (!vendorSellsWeenies.ContainsKey(parsed.shopVendorID))
                        //    vendorSellsWeenies.Add(parsed.shopVendorID, new List<uint>());

                        //vendorSellsWeenies[parsed.shopVendorID].Add(item.pwd._wcid);

                        if (!vendorSellsWeenies.ContainsKey(weenieId))
                            vendorSellsWeenies.Add(weenieId, new List<uint>());

                        if (!vendorSellsWeenies[weenieId].Contains(item.pwd._wcid))
                            vendorSellsWeenies[weenieId].Add(item.pwd._wcid);

                        if (!weeniesFromVendors.ContainsKey(item.pwd._wcid))
                        {
                            CreateObject newObj = GenerateCreateObjectfromVendorItemProfile(item, weeniesTypeTemplate);

                            if (newObj == null)
                                continue;

                            weeniesFromVendors.Add(item.pwd._wcid, newObj);
                        }
                    }

                    if (!vendorObjectIds.Contains(weenieId) && weenieId > 0)
                    {
                        CM_Vendor.gmVendorUI parsedClone;

                        parsedClone = new CM_Vendor.gmVendorUI();
                        parsedClone.shopVendorID = weenieId;

                        parsedClone.shopVendorProfile = parsed.shopVendorProfile;
                        parsedClone.shopItemProfileList = parsed.shopItemProfileList;

                        if (!vendorObjects.ContainsKey(fileToPutItIn))
                        {
                            vendorObjects.Add(fileToPutItIn, new Dictionary<uint, CM_Vendor.gmVendorUI>());
                        }

                        vendorObjects[fileToPutItIn].Add(parsedClone.shopVendorID, parsedClone);
                        vendorObjectIds.Add(parsedClone.shopVendorID);
                    }
                    totalHits++;
                }
            }
            catch (Exception ex)
            {
                totalExceptions++;
            }
        }

        private CreateObject GenerateCreateObjectfromVendorItemProfile(CM_Vendor.ItemProfile item, Dictionary<uint, CM_Physics.CreateObject> weeniesTypeTemplate)
        {
            try
            {
                CreateObject obj = new CreateObject();

                obj.object_id = item.iid;
                // obj.object_id = item.pwd._wcid;
                obj.wdesc = item.pwd;

                CreateObject template = new CreateObject();

                //if ((obj.wdesc._bitfield & (uint)CM_Physics.PublicWeenieDesc.BitfieldIndex.BF_LIFESTONE) != 0)
                //{

                //}
                //else if ((obj.wdesc._bitfield & (uint)CM_Physics.PublicWeenieDesc.BitfieldIndex.BF_BINDSTONE) != 0)
                //{

                //}
                //else if (obj.wdesc._wcid == 1)
                //{

                //}
                //else if ((obj.wdesc._bitfield & (uint)CM_Physics.PublicWeenieDesc.BitfieldIndex.BF_PKSWITCH) != 0)
                //{

                //}
                //else if ((obj.wdesc._bitfield & (uint)CM_Physics.PublicWeenieDesc.BitfieldIndex.BF_NPKSWITCH) != 0)
                //{

                //}            
                //else if ((obj.wdesc._bitfield & (uint)CM_Physics.PublicWeenieDesc.BitfieldIndex.BF_LOCKPICK) != 0)
                if ((obj.wdesc._bitfield & (uint)CM_Physics.PublicWeenieDesc.BitfieldIndex.BF_LOCKPICK) != 0)
                {
                    weeniesTypeTemplate.TryGetValue((uint)WeenieType.Lockpick_WeenieType, out template);
                }
                else if ((obj.wdesc._bitfield & (uint)CM_Physics.PublicWeenieDesc.BitfieldIndex.BF_FOOD) != 0)
                {
                    weeniesTypeTemplate.TryGetValue((uint)WeenieType.Food_WeenieType, out template);
                }
                else if ((obj.wdesc._bitfield & (uint)CM_Physics.PublicWeenieDesc.BitfieldIndex.BF_HEALER) != 0)
                {
                    weeniesTypeTemplate.TryGetValue((uint)WeenieType.Healer_WeenieType, out template);
                }
                else if ((obj.wdesc._bitfield & (uint)CM_Physics.PublicWeenieDesc.BitfieldIndex.BF_BOOK) != 0)
                {
                    weeniesTypeTemplate.TryGetValue((uint)WeenieType.Book_WeenieType, out template);
                    if (obj.wdesc._name.m_buffer.Contains("Statue"))
                    {

                    }
                    else if (obj.wdesc._name.m_buffer.Contains("Scroll"))
                    {

                    }
                    else if (obj.wdesc._name.m_buffer.Contains("Pack"))
                    {

                    }
                    else if (obj.wdesc._wcid == 9002)
                    {

                    }
                    else if (obj.wdesc._wcid == 12774
                        || obj.wdesc._wcid == 16908
                        )
                    {

                    }
                    else if (obj.object_id < 0x80000000)
                    {

                    }
                    else
                    {

                    }
                }
                //else if ((obj.wdesc._bitfield & (uint)CM_Physics.PublicWeenieDesc.BitfieldIndex.BF_PORTAL) != 0)
                //{
                //    if (
                //        obj.wdesc._wcid == 9620 || // W_PORTALHOUSE_CLASS
                //        obj.wdesc._wcid == 10751 || // W_PORTALHOUSETEST_CLASS
                //        obj.wdesc._wcid == 11730    // W_HOUSEPORTAL_CLASS
                //        )
                //    {

                //    }
                //    else if (obj.wdesc._wcid == 1955)
                //    {

                //    }
                //    else if (obj.wdesc._name.m_buffer.Contains("Town Network"))
                //    {

                //    }
                //    else if (obj.wdesc._name.m_buffer.Contains("Floating City"))
                //    {

                //    }
                //    else if (obj.wdesc._name.m_buffer.Contains("Humming Crystal"))
                //    {

                //    }
                //    else if (obj.wdesc._name.m_buffer.Contains("The Orphanage"))
                //    {

                //    }
                //    else if (obj.wdesc._name.m_buffer.Contains("Golem Sanctum"))
                //    {

                //    }
                //    else if (obj.wdesc._name.m_buffer.Contains("Destroyed"))
                //    {

                //    }
                //    else if (obj.wdesc._name.m_buffer.Contains("Meeting Hall"))
                //    {

                //    }
                //    else if (obj.wdesc._name.m_buffer.Contains("Portal to"))
                //    {

                //    }
                //    else if (obj.wdesc._name.m_buffer.Contains("Portal"))
                //    {

                //    }
                //    else
                //    {

                //    }
                //}
                //else if ((obj.wdesc._bitfield & (uint)CM_Physics.PublicWeenieDesc.BitfieldIndex.BF_DOOR) != 0)
                //{
                //    if (obj.wdesc._wcid == 412
                //        )
                //    {

                //    }
                //    else if (obj.wdesc._wcid == 15451)
                //    {

                //    }
                //    else if (obj.wdesc._wcid == 577)
                //    {

                //    }
                //    else
                //    {

                //    }
                //}
                //else if ((obj.wdesc._bitfield & (uint)CM_Physics.PublicWeenieDesc.BitfieldIndex.BF_VENDOR) != 0)
                //{
                //    if (obj.wdesc._name.m_buffer == "Babe the Blue Auroch"
                //        || obj.wdesc._name.m_buffer == "Paul the Monouga"
                //        )
                //    {

                //    }
                //    else if (obj.wdesc._wcid == 43481
                //        || obj.wdesc._wcid == 43480
                //        )
                //    {

                //    }
                //    else if (obj.wdesc._name.m_buffer.Contains("Crier")
                //        && obj.wdesc._blipColor == 8)
                //    {

                //    }
                //    else if (obj.wdesc._name.m_buffer.Contains("Pet")
                //        || obj.wdesc._name.m_buffer.Contains("Wind-up")
                //        || obj.wdesc._wcid == 48881
                //        || obj.wdesc._wcid == 34902
                //        || obj.wdesc._wcid == 48891
                //        || obj.wdesc._wcid == 48879
                //        || obj.wdesc._wcid == 34906
                //        || obj.wdesc._wcid == 48887
                //        || obj.wdesc._wcid == 48889
                //        || obj.wdesc._wcid == 48883
                //        || obj.wdesc._wcid == 34900
                //        || obj.wdesc._wcid == 34901
                //        || obj.wdesc._wcid == 34908
                //        || obj.wdesc._wcid == 34898
                //        )
                //    {

                //    }
                //    else if (obj.wdesc._blipColor == 8)
                //    {

                //    }
                //    else
                //    {

                //    }
                //}
                //else if (obj.wdesc._wcid == 4)
                //{

                //}
                else if (obj.wdesc._type == ITEM_TYPE.TYPE_MISC)
                {
                    weeniesTypeTemplate.TryGetValue((uint)WeenieType.Generic_WeenieType, out template);
                    if (
                           obj.wdesc._wcid == 9548 || // W_HOUSE_CLASS
                           obj.wdesc._wcid >= 9693 && obj.wdesc._wcid <= 10492 || // W_HOUSECOTTAGE1_CLASS to W_HOUSECOTTAGE800_CLASS
                           obj.wdesc._wcid >= 10493 && obj.wdesc._wcid <= 10662 || // W_HOUSEVILLA801_CLASS to W_HOUSEVILLA970_CLASS
                           obj.wdesc._wcid >= 10663 && obj.wdesc._wcid <= 10692 || // W_HOUSEMANSION971_CLASS to W_HOUSEMANSION1000_CLASS
                           obj.wdesc._wcid >= 10746 && obj.wdesc._wcid <= 10750 || // W_HOUSETEST1_CLASS to W_HOUSETEST5_CLASS
                           obj.wdesc._wcid >= 10829 && obj.wdesc._wcid <= 10839 || // W_HOUSETEST6_CLASS to W_HOUSETEST16_CLASS
                           obj.wdesc._wcid >= 11677 && obj.wdesc._wcid <= 11682 || // W_HOUSETEST17_CLASS to W_HOUSETEST22_CLASS
                           obj.wdesc._wcid >= 12311 && obj.wdesc._wcid <= 12460 || // W_HOUSECOTTAGE1001_CLASS to W_HOUSECOTTAGE1150_CLASS
                           obj.wdesc._wcid >= 12775 && obj.wdesc._wcid <= 13024 || // W_HOUSECOTTAGE1151_CLASS to W_HOUSECOTTAGE1400_CLASS
                           obj.wdesc._wcid >= 13025 && obj.wdesc._wcid <= 13064 || // W_HOUSEVILLA1401_CLASS to W_HOUSEVILLA1440_CLASS
                           obj.wdesc._wcid >= 13065 && obj.wdesc._wcid <= 13074 || // W_HOUSEMANSION1441_CLASS to W_HOUSEMANSION1450_CLASS
                           obj.wdesc._wcid == 13234 || // W_HOUSECOTTAGETEST10000_CLASS
                           obj.wdesc._wcid == 13235 || // W_HOUSEVILLATEST10001_CLASS
                           obj.wdesc._wcid >= 13243 && obj.wdesc._wcid <= 14042 || // W_HOUSECOTTAGE1451_CLASS to W_HOUSECOTTAGE2350_CLASS
                           obj.wdesc._wcid >= 14043 && obj.wdesc._wcid <= 14222 || // W_HOUSEVILLA1851_CLASS to W_HOUSEVILLA2440_CLASS
                           obj.wdesc._wcid >= 14223 && obj.wdesc._wcid <= 14242 || // W_HOUSEMANSION1941_CLASS to W_HOUSEMANSION2450_CLASS
                           obj.wdesc._wcid >= 14938 && obj.wdesc._wcid <= 15087 || // W_HOUSECOTTAGE2451_CLASS to W_HOUSECOTTAGE2600_CLASS
                           obj.wdesc._wcid >= 15088 && obj.wdesc._wcid <= 15127 || // W_HOUSEVILLA2601_CLASS to W_HOUSEVILLA2640_CLASS
                           obj.wdesc._wcid >= 15128 && obj.wdesc._wcid <= 15137 || // W_HOUSEMANSION2641_CLASS to W_HOUSEMANSION2650_CLASS
                           obj.wdesc._wcid >= 15452 && obj.wdesc._wcid <= 15457 || // W_HOUSEAPARTMENT2851_CLASS to W_HOUSEAPARTMENT2856_CLASS
                           obj.wdesc._wcid >= 15458 && obj.wdesc._wcid <= 15607 || // W_HOUSECOTTAGE2651_CLASS to W_HOUSECOTTAGE2800_CLASS
                           obj.wdesc._wcid >= 15612 && obj.wdesc._wcid <= 15661 || // W_HOUSEVILLA2801_CLASS to W_HOUSEVILLA2850_CLASS
                           obj.wdesc._wcid >= 15897 && obj.wdesc._wcid <= 16890 || // W_HOUSEAPARTMENT2857_CLASS to W_HOUSEAPARTMENT3850_CLASS
                           obj.wdesc._wcid >= 16923 && obj.wdesc._wcid <= 18923 || // W_HOUSEAPARTMENT4051_CLASS to W_HOUSEAPARTMENT6050_CLASS
                           obj.wdesc._wcid >= 18924 && obj.wdesc._wcid <= 19073 || // W_HOUSECOTTAGE3851_CLASS to W_HOUSECOTTAGE4000_CLASS
                           obj.wdesc._wcid >= 19077 && obj.wdesc._wcid <= 19126 || // W_HOUSEVILLA4001_CLASS to W_HOUSEVILLA4050_CLASS
                           obj.wdesc._wcid >= 20650 && obj.wdesc._wcid <= 20799 || // W_HOUSECOTTAGE6051_CLASS to W_HOUSECOTTAGE6200_CLASS
                           obj.wdesc._wcid >= 20800 && obj.wdesc._wcid <= 20839 || // W_HOUSEVILLA6201_CLASS to W_HOUSEVILLA6240_CLASS
                           obj.wdesc._wcid >= 20840 && obj.wdesc._wcid <= 20849    // W_HOUSEMANSION6241_CLASS to W_HOUSEMANSION6250_CLASS
                           )
                    {
                        weeniesTypeTemplate.TryGetValue((uint)WeenieType.House_WeenieType, out template);
                    }
                    //else if (parsed.wdesc._name.m_buffer.Contains("Essence"))
                    //{
                    //    weeniefileToPutItIn = "Essences";
                    //    addWeenie = true;
                    //}
                    //else if (parsed.wdesc._name.m_buffer.Contains("Spirit"))
                    //{
                    //    weeniefileToPutItIn = "Spirits";
                    //    addWeenie = true;
                    //}
                    //else if (obj.physicsdesc.setup_id == 33555088
                    //    || obj.physicsdesc.setup_id == 33557390
                    //    || obj.physicsdesc.setup_id == 33555594
                    //    || obj.physicsdesc.setup_id == 33555909
                    //    )
                    //{

                    //}
                    else if (obj.wdesc._name.m_buffer.Contains("Residential Halls"))
                    {

                    }
                    else if (obj.wdesc._name.m_buffer.Contains("Deed"))
                    {
                        weeniesTypeTemplate.TryGetValue((uint)WeenieType.Deed_WeenieType, out template);
                    }
                    else if (obj.wdesc._name.m_buffer.Contains("Court")
                        || obj.wdesc._name.m_buffer.Contains("Dwellings")
                        || obj.wdesc._name.m_buffer.Contains("SylvanDwellings")
                        || obj.wdesc._name.m_buffer.Contains("Veranda")
                        || obj.wdesc._name.m_buffer.Contains("Gate")
                        || (obj.wdesc._name.m_buffer.Contains("Yard") && !obj.wdesc._name.m_buffer.Contains("Balloons"))
                        || obj.wdesc._name.m_buffer.Contains("Gardens")
                        || obj.wdesc._name.m_buffer.Contains("Lodge")
                        || obj.wdesc._name.m_buffer.Contains("Grotto")
                        || (obj.wdesc._name.m_buffer.Contains("Hollow") && !obj.wdesc._name.m_buffer.Contains("Minion"))
                        )
                    {

                    }
                    else if (obj.wdesc._name.m_buffer.Contains("Festival Stone"))
                    {

                    }
                    //else if (obj.physicsdesc.setup_id == 33557463
                    //    )
                    //{

                    //}
                    else if (obj.wdesc._name.m_buffer.Contains("Button"))
                    {
                        weeniesTypeTemplate.TryGetValue((uint)WeenieType.Switch_WeenieType, out template);
                    }
                    else if (obj.wdesc._name.m_buffer.Contains("Lever")
                        || obj.wdesc._name.m_buffer.Contains("Candle") && !obj.wdesc._name.m_buffer.Contains("Floating") && !obj.wdesc._name.m_buffer.Contains("Bronze")
                        || obj.wdesc._name.m_buffer.Contains("Torch") && obj.wdesc._wcid != 293
                        || obj.wdesc._name.m_buffer.Contains("Plant") && !obj.wdesc._name.m_buffer.Contains("Fertilized")
                        )
                    {
                        weeniesTypeTemplate.TryGetValue((uint)WeenieType.Switch_WeenieType, out template);
                    }
                    else if (obj.object_id < 0x80000000)
                    {

                    }
                    else
                    {

                    }
                }
                //else if (obj.wdesc._type == ITEM_TYPE.TYPE_PORTAL) // HOUSE PORTALS
                //{
                //    if (
                //        obj.wdesc._wcid == 9620 || // W_PORTALHOUSE_CLASS
                //        obj.wdesc._wcid == 10751 || // W_PORTALHOUSETEST_CLASS
                //        obj.wdesc._wcid == 11730    // W_HOUSEPORTAL_CLASS
                //                            )
                //    {

                //    }
                //    else if (obj.wdesc._wcid == 1955)
                //    {

                //    }
                //    else if (obj.wdesc._name.m_buffer.Contains("Town Network"))
                //    {

                //    }
                //    else if (obj.wdesc._name.m_buffer.Contains("Floating City"))
                //    {

                //    }
                //    else if (obj.wdesc._name.m_buffer.Contains("Humming Crystal"))
                //    {

                //    }
                //    else if (obj.wdesc._name.m_buffer.Contains("The Orphanage"))
                //    {

                //    }
                //    else if (obj.wdesc._name.m_buffer.Contains("Golem Sanctum"))
                //    {

                //    }
                //    else if (obj.wdesc._name.m_buffer.Contains("Destroyed"))
                //    {

                //    }
                //    else if (obj.wdesc._name.m_buffer.Contains("Meeting Hall"))
                //    {

                //    }
                //    else if (obj.wdesc._name.m_buffer.Contains("Portal to"))
                //    {

                //    }
                //    else if (obj.wdesc._name.m_buffer.Contains("Portal"))
                //    {

                //    }
                //    else
                //    {

                //    }
                //}
                else if (obj.wdesc._type == ITEM_TYPE.TYPE_CONTAINER) // HOOKS AND STORAGE
                {
                    weeniesTypeTemplate.TryGetValue((uint)WeenieType.Container_WeenieType, out template);
                    if (
                        obj.wdesc._wcid == 9686 && obj.wdesc._name.m_buffer.Contains("Hook") || // W_HOOK_CLASS
                        obj.wdesc._wcid == 11697 && obj.wdesc._name.m_buffer.Contains("Hook") || // W_HOOK_FLOOR_CLASS
                        obj.wdesc._wcid == 11698 && obj.wdesc._name.m_buffer.Contains("Hook") || // W_HOOK_CEILING_CLASS
                        obj.wdesc._wcid == 12678 && obj.wdesc._name.m_buffer.Contains("Hook") || // W_HOOK_ROOF_CLASS
                        obj.wdesc._wcid == 12679 && obj.wdesc._name.m_buffer.Contains("Hook") // W_HOOK_YARD_CLASS
                        )
                    {
                        weeniesTypeTemplate.TryGetValue((uint)WeenieType.Hook_WeenieType, out template);
                    }
                    else if (
                            obj.wdesc._wcid == 9686 || // W_HOOK_CLASS
                            obj.wdesc._wcid == 11697 || // W_HOOK_FLOOR_CLASS
                            obj.wdesc._wcid == 11698 || // W_HOOK_CEILING_CLASS
                            obj.wdesc._wcid == 12678 || // W_HOOK_ROOF_CLASS
                            obj.wdesc._wcid == 12679  // W_HOOK_YARD_CLASS
                            )
                    {
                        weeniesTypeTemplate.TryGetValue((uint)WeenieType.Hook_WeenieType, out template);
                    }
                    else if (
                            obj.wdesc._wcid == 9687     // W_STORAGE_CLASS
                            )
                    {
                        weeniesTypeTemplate.TryGetValue((uint)WeenieType.Storage_WeenieType, out template);
                    }
                    else if (obj.wdesc._wcid == 21)
                    {
                        weeniesTypeTemplate.TryGetValue((uint)WeenieType.Corpse_WeenieType, out template);
                    }
                    //else if (parsed.wdesc._name.m_buffer.Contains("Corpse"))
                    //{
                    //    fileToPutItIn = "Corpses";
                    //    addIt = true;
                    //}
                    else if (obj.wdesc._name.m_buffer.Contains("Standing Stone"))
                    {

                    }
                    else if (obj.wdesc._name.m_buffer.Contains("Pack")
                        || obj.wdesc._name.m_buffer.Contains("Backpack")
                        || obj.wdesc._name.m_buffer.Contains("Sack")
                        || obj.wdesc._name.m_buffer.Contains("Pouch")
                        )
                    {
                    }
                    else if (
                        obj.wdesc._name.m_buffer.Contains("Chest")
                        || obj.wdesc._name.m_buffer.Contains("Coffer")
                        || obj.wdesc._name.m_buffer.Contains("Vault")
                        || obj.wdesc._name.m_buffer.Contains("Storage")
                        || obj.wdesc._name.m_buffer.Contains("Stump")
                        || obj.wdesc._name.m_buffer.Contains("Shelf")
                        || obj.wdesc._name.m_buffer.Contains("Reliquary")
                        || obj.wdesc._name.m_buffer.Contains("Crate")
                        || obj.wdesc._name.m_buffer.Contains("Cache")
                        || obj.wdesc._name.m_buffer.Contains("Tomb")
                        || obj.wdesc._name.m_buffer.Contains("Sarcophagus")
                        || obj.wdesc._name.m_buffer.Contains("Footlocker")
                        || obj.wdesc._name.m_buffer.Contains("Holding")
                        || obj.wdesc._name.m_buffer.Contains("Wheelbarrow")
                        || obj.wdesc._name.m_buffer.Contains("Stash")
                        || obj.wdesc._name.m_buffer.Contains("Trove")
                        || obj.wdesc._name.m_buffer.Contains("Prism")
                        || obj.wdesc._name.m_buffer.Contains("Strongbox")
                        || obj.wdesc._name.m_buffer.Contains("Supplies")
                        )
                    {
                        weeniesTypeTemplate.TryGetValue((uint)WeenieType.Chest_WeenieType, out template);
                    }
                    else if (obj.object_id < 0x80000000)
                    {

                    }
                    else
                    {

                    }
                }
                else if (obj.wdesc._type == ITEM_TYPE.TYPE_UNDEF) // SLUMLORD OBJECTS
                {
                    weeniesTypeTemplate.TryGetValue((uint)WeenieType.Generic_WeenieType, out template);
                    if (
                        obj.wdesc._wcid == 9621 || // W_SLUMLORD_CLASS
                        obj.wdesc._wcid == 10752 || // W_SLUMLORDTESTCHEAP_CLASS
                        obj.wdesc._wcid == 10753 || // W_SLUMLORDTESTEXPENSIVE_CLASS
                        obj.wdesc._wcid == 10754 || // W_SLUMLORDTESTMODERATE_CLASS
                        obj.wdesc._wcid == 11711 || // W_SLUMLORDCOTTAGECHEAP_CLASS
                        obj.wdesc._wcid == 11712 || // W_SLUMLORDCOTTAGEEXPENSIVE_CLASS
                        obj.wdesc._wcid == 11713 || // W_SLUMLORDCOTTAGEMODERATE_CLASS
                        obj.wdesc._wcid == 11714 || // W_SLUMLORDMANSIONCHEAP_CLASS
                        obj.wdesc._wcid == 11715 || // W_SLUMLORDMANSIONEXPENSIVE_CLASS
                        obj.wdesc._wcid == 11716 || // W_SLUMLORDMANSIONMODERATE_CLASS
                        obj.wdesc._wcid == 11717 || // W_SLUMLORDVILLACHEAP_CLASS
                        obj.wdesc._wcid == 11718 || // W_SLUMLORDVILLAEXPENSIVE_CLASS
                        obj.wdesc._wcid == 11719 || // W_SLUMLORDVILLAMODERATE_CLASS
                        obj.wdesc._wcid == 11977 || // W_SLUMLORDCOTTAGES349_579_CLASS
                        obj.wdesc._wcid == 11978 || // W_SLUMLORDVILLA851_925_CLASS
                        obj.wdesc._wcid == 11979 || // W_SLUMLORDCOTTAGE580_800_CLASS
                        obj.wdesc._wcid == 11980 || // W_SLUMLORDVILLA926_970_CLASS
                        obj.wdesc._wcid == 11980 || // W_SLUMLORDVILLA926_970_CLASS
                        obj.wdesc._wcid == 12461 || // W_SLUMLORDCOTTAGE1001_1075_CLASS
                        obj.wdesc._wcid == 12462 || // W_SLUMLORDCOTTAGE1076_1150_CLASS
                        obj.wdesc._wcid == 13078 || // W_SLUMLORDCOTTAGE1151_1275_CLASS
                        obj.wdesc._wcid == 13079 || // W_SLUMLORDCOTTAGE1276_1400_CLASS
                        obj.wdesc._wcid == 13080 || // W_SLUMLORDVILLA1401_1440_CLASS
                        obj.wdesc._wcid == 13081 || // W_SLUMLORDMANSION1441_1450_CLASS
                        obj.wdesc._wcid == 14243 || // W_SLUMLORDCOTTAGE1451_1650_CLASS
                        obj.wdesc._wcid == 14244 || // W_SLUMLORDCOTTAGE1651_1850_CLASS
                        obj.wdesc._wcid == 14245 || // W_SLUMLORDVILLA1851_1940_CLASS
                        obj.wdesc._wcid == 14246 || // W_SLUMLORDMANSION1941_1950_CLASS
                        obj.wdesc._wcid == 14247 || // W_SLUMLORDCOTTAGE1951_2150_CLASS
                        obj.wdesc._wcid == 14248 || // W_SLUMLORDCOTTAGE2151_2350_CLASS
                        obj.wdesc._wcid == 14249 || // W_SLUMLORDVILLA2351_2440_CLASS
                        obj.wdesc._wcid == 14250 || // W_SLUMLORDMANSION2441_2450_CLASS
                        obj.wdesc._wcid == 14934 || // W_SLUMLORDCOTTAGE2451_2525_CLASS
                        obj.wdesc._wcid == 14935 || // W_SLUMLORDCOTTAGE2526_2600_CLASS
                        obj.wdesc._wcid == 14936 || // W_SLUMLORDVILLA2601_2640_CLASS
                        obj.wdesc._wcid == 14937 || // W_SLUMLORDMANSION2641_2650_CLASS
                                                    // parsed.wdesc._wcid == 15273 || // W_SLUMLORDFAKENUHMUDIRA_CLASS
                        obj.wdesc._wcid == 15608 || // W_SLUMLORDAPARTMENT_CLASS
                        obj.wdesc._wcid == 15609 || // W_SLUMLORDCOTTAGE2651_2725_CLASS
                        obj.wdesc._wcid == 15610 || // W_SLUMLORDCOTTAGE2726_2800_CLASS
                        obj.wdesc._wcid == 15611 || // W_SLUMLORDVILLA2801_2850_CLASS
                        obj.wdesc._wcid == 19074 || // W_SLUMLORDCOTTAGE3851_3925_CLASS
                        obj.wdesc._wcid == 19075 || // W_SLUMLORDCOTTAGE3926_4000_CLASS
                        obj.wdesc._wcid == 19076 || // W_SLUMLORDVILLA4001_4050_CLASS
                        obj.wdesc._wcid == 20850 || // W_SLUMLORDCOTTAGE6051_6125_CLASS
                        obj.wdesc._wcid == 20851 || // W_SLUMLORDCOTTAGE6126_6200_CLASS
                        obj.wdesc._wcid == 20852 || // W_SLUMLORDVILLA6201_6240_CLASS
                        obj.wdesc._wcid == 20853    // W_SLUMLORDMANSION6241_6250_CLASS
                                                    // parsed.wdesc._wcid == 22118 || // W_SLUMLORDHAUNTEDMANSION_CLASS
                        )
                    {
                        weeniesTypeTemplate.TryGetValue((uint)WeenieType.SlumLord_WeenieType, out template);
                    }

                    else if (
                                                        obj.wdesc._wcid == 15273 || // W_SLUMLORDFAKENUHMUDIRA_CLASS
                                                        obj.wdesc._wcid == 22118    // W_SLUMLORDHAUNTEDMANSION_CLASS
                        )
                    {

                    }
                    else if (obj.wdesc._wcid == 10762)
                    {

                    }
                    else if (obj.wdesc._name.m_buffer.Contains("Gen")
                        )
                    {

                    }
                    //else if (
                    //    parsed.wdesc._name.m_buffer.Contains("Bolt")
                    //    || parsed.wdesc._name.m_buffer.Contains("wave")
                    //    || parsed.wdesc._name.m_buffer.Contains("Wave")
                    //    || parsed.wdesc._name.m_buffer.Contains("Blast")
                    //    || parsed.wdesc._name.m_buffer.Contains("Ring")
                    //    || parsed.wdesc._name.m_buffer.Contains("Stream")
                    //    || parsed.wdesc._name.m_buffer.Contains("Fist")
                    //    || parsed.wdesc._name.m_buffer.Contains("Missile")
                    //    || parsed.wdesc._name.m_buffer.Contains("Egg")
                    //    || parsed.wdesc._name.m_buffer.Contains("Death")
                    //    || parsed.wdesc._name.m_buffer.Contains("Fury")
                    //     || parsed.wdesc._name.m_buffer.Contains("Wind")
                    //    || parsed.wdesc._name.m_buffer.Contains("Flaming Skull")
                    //     || parsed.wdesc._name.m_buffer.Contains("Edge")
                    //    || parsed.wdesc._name.m_buffer.Contains("Snowball")
                    //    || parsed.wdesc._name.m_buffer.Contains("Bomb")
                    //    || parsed.wdesc._name.m_buffer.Contains("Blade")
                    //    || parsed.wdesc._name.m_buffer.Contains("Stalactite")
                    //    || parsed.wdesc._name.m_buffer.Contains("Boulder")
                    //    || parsed.wdesc._name.m_buffer.Contains("Whirlwind")
                    //    )
                    //{
                    //    weeniefileToPutItIn = "UndefObjects";
                    //    addWeenie = true;
                    //}
                    else if (obj.wdesc._name.m_buffer.Contains("Generator"))
                    {

                    }
                    //else if (parsed.wdesc._name.m_buffer.Contains("Rabbit"))
                    //{
                    //    fileToPutItIn = "UndefRabbits";
                    //    addIt = true;
                    //}
                    else if (
                           obj.wdesc._name.m_buffer.Contains("Bolt")
                        || obj.wdesc._name.m_buffer.Contains("wave")
                        || obj.wdesc._name.m_buffer.Contains("Wave")
                        || obj.wdesc._name.m_buffer.Contains("Blast")
                        || obj.wdesc._name.m_buffer.Contains("Ring")
                        || obj.wdesc._name.m_buffer.Contains("Stream")
                        || obj.wdesc._name.m_buffer.Contains("Fist")
                        // || parsed.wdesc._name.m_buffer.Contains("Missile")
                        // || parsed.wdesc._name.m_buffer.Contains("Egg")
                        || obj.wdesc._name.m_buffer.Contains("Death")
                        || obj.wdesc._name.m_buffer.Contains("Fury")
                         || obj.wdesc._name.m_buffer.Contains("Wind")
                        || obj.wdesc._name.m_buffer.Contains("Flaming Skull")
                         || obj.wdesc._name.m_buffer.Contains("Edge")
                        // || parsed.wdesc._name.m_buffer.Contains("Snowball")
                        || obj.wdesc._name.m_buffer.Contains("Bomb")
                        || obj.wdesc._name.m_buffer.Contains("Blade")
                        || obj.wdesc._name.m_buffer.Contains("Stalactite")
                        || obj.wdesc._name.m_buffer.Contains("Boulder")
                        || obj.wdesc._name.m_buffer.Contains("Whirlwind")
                    )
                    {

                    }
                    else if (obj.wdesc._name.m_buffer.Contains("Missile")
                            || obj.wdesc._name.m_buffer.Contains("Egg")
                            || obj.wdesc._name.m_buffer.Contains("Snowball")
                    )
                    {

                    }
                    else if (obj.object_id < 0x80000000)
                    {

                    }
                    else
                    {

                    }
                }
                else if (obj.wdesc._type == ITEM_TYPE.TYPE_WRITABLE)
                {
                    weeniesTypeTemplate.TryGetValue((uint)WeenieType.Generic_WeenieType, out template);
                    if (obj.wdesc._name.m_buffer.Contains("Statue"))
                    {

                    }
                    else if (obj.wdesc._name.m_buffer.Contains("Scroll")
                        || obj.wdesc._name.m_buffer.Contains("Aura")
                        || obj.wdesc._name.m_buffer.Contains("Recall")
                        || obj.wdesc._name.m_buffer.Contains("Inscription")
                        )
                    {
                        weeniesTypeTemplate.TryGetValue((uint)WeenieType.Scroll_WeenieType, out template);
                    }
                    else if (obj.wdesc._name.m_buffer.Contains("Pack"))
                    {

                    }
                    else if (obj.wdesc._wcid == 9002)
                    {

                    }
                    else if (obj.object_id < 0x80000000)
                    {

                    }
                    else
                    {

                    }
                }
                //else if (obj.wdesc._type == ITEM_TYPE.TYPE_LIFESTONE)
                //{

                //}
                ////else if ((parsed.wdesc._name.m_buffer.Contains("Scrivener")
                ////        || parsed.wdesc._name.m_buffer.Contains("Scribe")
                ////        || parsed.wdesc._name.m_buffer.Contains("Archmage")
                ////        || parsed.wdesc._name.m_buffer.Contains("Healer")
                ////        || parsed.wdesc._name.m_buffer.Contains("Weaponsmith")
                ////        || parsed.wdesc._name.m_buffer.Contains("Weapons Master")
                ////        || parsed.wdesc._name.m_buffer.Contains("Armorer")
                ////        || parsed.wdesc._name.m_buffer.Contains("Grocer")
                ////        || parsed.wdesc._name.m_buffer.Contains("Shopkeep")
                ////        || parsed.wdesc._name.m_buffer.Contains("Shopkeeper")
                ////        || parsed.wdesc._name.m_buffer.Contains("Jeweler")
                ////        || parsed.wdesc._name.m_buffer.Contains("Barkeep")
                ////        || parsed.wdesc._name.m_buffer.Contains("Barkeeper")
                ////        || parsed.wdesc._name.m_buffer.Contains("Provisioner")
                ////        || parsed.wdesc._name.m_buffer.Contains("Tailor")
                ////        || parsed.wdesc._name.m_buffer.Contains("Seamstress")
                ////        || parsed.wdesc._name.m_buffer.Contains("Fletcher")
                ////        || parsed.wdesc._name.m_buffer.Contains("Bowyer")
                ////        || parsed.wdesc._name.m_buffer.Contains("Marksman")
                ////        || parsed.wdesc._name.m_buffer.Contains("Crafter")
                ////        || parsed.wdesc._name.m_buffer.Contains("Cook")
                ////        || parsed.wdesc._name.m_buffer.Contains("Alchemist")
                ////        || parsed.wdesc._name.m_buffer.Contains("Woodsman")
                ////        || parsed.wdesc._name.m_buffer.Contains("Apprentice"))
                ////    && parsed.wdesc._type == ITEM_TYPE.TYPE_CREATURE
                ////    && parsed.wdesc._blipColor == 8)
                ////{
                ////    fileToPutItIn = "Vendors";
                ////    addIt = true;
                ////}
                ////else if ((parsed.wdesc._name.m_buffer == "Agent of the Arcanum"
                ////        || parsed.wdesc._name.m_buffer == "Sentry"
                ////        || parsed.wdesc._name.m_buffer == "Ulgrim the Unpleasant"
                ////        || parsed.wdesc._name.m_buffer.Contains("Ulgrim")
                ////        || parsed.wdesc._name.m_buffer == "Ned the Clever"
                ////        || parsed.wdesc._name.m_buffer == "Wedding Planner"
                ////        || parsed.wdesc._name.m_buffer.Contains("Collector")
                ////        || parsed.wdesc._name.m_buffer.Contains("Guard")
                ////        || parsed.wdesc._name.m_buffer == "Jonathan"
                ////        || parsed.wdesc._name.m_buffer == "Farmer")
                ////    && parsed.wdesc._type == ITEM_TYPE.TYPE_CREATURE
                ////    && parsed.wdesc._blipColor == 8)
                ////{
                ////    fileToPutItIn = "OtherNPCs";
                ////    addIt = true;
                ////}
                else if (obj.wdesc._type == ITEM_TYPE.TYPE_CREATURE)
                {
                    weeniesTypeTemplate.TryGetValue((uint)WeenieType.Creature_WeenieType, out template);
                    if (//parsed.wdesc._name.m_buffer == "The Chicken"
                        //|| parsed.wdesc._name.m_buffer == "Babe the Blue Auroch"
                        obj.wdesc._name.m_buffer == "Babe the Blue Auroch"
                        || obj.wdesc._name.m_buffer == "Paul the Monouga"
                        //|| parsed.wdesc._name.m_buffer == "Silencia's Magma Golem"
                        //|| parsed.wdesc._name.m_buffer == "Repair Golem"
                        )
                    {

                    }
                    else if (obj.wdesc._wcid == 43481
                        || obj.wdesc._wcid == 43480
                        )
                    {

                    }
                    else if (obj.wdesc._name.m_buffer.Contains("Crier")
                        && obj.wdesc._blipColor == 8)
                    {

                    }
                    else if (obj.wdesc._name.m_buffer.Contains("Pet")
                        || obj.wdesc._name.m_buffer.Contains("Wind-up")
                        || obj.wdesc._wcid == 48881
                        || obj.wdesc._wcid == 34902
                        || obj.wdesc._wcid == 48891
                        || obj.wdesc._wcid == 48879
                        || obj.wdesc._wcid == 34906
                        || obj.wdesc._wcid == 48887
                        || obj.wdesc._wcid == 48889
                        || obj.wdesc._wcid == 48883
                        || obj.wdesc._wcid == 34900
                        || obj.wdesc._wcid == 34901
                        || obj.wdesc._wcid == 34908
                        || obj.wdesc._wcid == 34898
                        )
                    {
                        weeniesTypeTemplate.TryGetValue((uint)WeenieType.PetDevice_WeenieType, out template);
                    }
                    else if (obj.wdesc._blipColor == 8)
                    {

                    }
                    else if (obj.wdesc._blipColor == 2)
                    {

                    }
                    else if (obj.object_id < 0x80000000)
                    {

                    }
                    else if (obj.wdesc._name.m_buffer.Contains("Statue") && !obj.wdesc._name.m_buffer.Contains("Bronze")
                        || obj.wdesc._name.m_buffer.Contains("Shrine")
                        // || parsed.wdesc._name.m_buffer.Contains("Altar")
                        || obj.wdesc._name.m_buffer.Contains("Warden of")
                        || obj.wdesc._name.m_buffer.Contains("Device")
                        || obj.wdesc._name.m_buffer.Contains("Seed")
                        || obj.wdesc._name.m_buffer.Contains("Forge")
                        || obj.wdesc._name.m_buffer.Contains("Tower Guardian")
                        || obj.wdesc._name.m_buffer.Contains("New Aluvian Champion")
                        || obj.wdesc._name.m_buffer.Contains("Barrel")
                        || obj.wdesc._name.m_buffer.Contains("New Aluvian War Mage Champion")
                        || obj.wdesc._name.m_buffer.Contains("Wounded Drudge Skulker")
                        || obj.wdesc._name.m_buffer.Contains("Servant of")
                        || obj.wdesc._name.m_buffer.Contains("Prison")
                        || obj.wdesc._name.m_buffer.Contains("Temple")
                        || obj.wdesc._name.m_buffer.Contains("Mana Siphon")
                        || obj.wdesc._name.m_buffer.Contains("Mnemosyne")
                        || obj.wdesc._name.m_buffer.Contains("Portal")
                        || obj.wdesc._name.m_buffer.Contains("Door")
                        || obj.wdesc._name.m_buffer.Contains("Wall")
                        || obj.wdesc._name.m_buffer.Contains("Pit")
                        || obj.wdesc._name.m_buffer.Contains("Book")
                        || obj.wdesc._name.m_buffer.Contains("The Deep")
                        // || parsed.wdesc._name.m_buffer.Contains("Warner Brother")
                        || obj.wdesc._name.m_buffer.Contains("Fishing")
                        || obj.wdesc._name.m_buffer.Contains("Bookshelf")
                        || obj.wdesc._name.m_buffer.Contains("Cavern")
                        || obj.wdesc._name.m_buffer.Contains("Sword of Frozen Fury")
                        || obj.wdesc._name.m_buffer.Contains("Coffin")
                        || obj.wdesc._name.m_buffer.Contains("Silence")
                        || obj.wdesc._name.m_buffer == "Black"
                        || obj.wdesc._name.m_buffer.Contains("Eyes")
                        || obj.wdesc._name.m_buffer.Contains("Bed")
                        || obj.wdesc._name.m_buffer.Contains("Hole")
                        || obj.wdesc._name.m_buffer.Contains("Tribunal")
                        || obj.wdesc._name.m_buffer.Contains("Sunlight")
                        || obj.wdesc._name.m_buffer.Contains("Wind")
                        || obj.wdesc._name.m_buffer == "E"
                        || obj.wdesc._name.m_buffer == "Flame"
                        || obj.wdesc._name.m_buffer == "Death"
                        || obj.wdesc._name.m_buffer == "Darkness"
                        || obj.wdesc._name.m_buffer == "Time"
                        || obj.wdesc._name.m_buffer == "Ring"
                        || obj.wdesc._name.m_buffer == "Hope"
                        || obj.wdesc._name.m_buffer == "Mushroom"
                        || obj.wdesc._name.m_buffer == "Stars"
                        || obj.wdesc._name.m_buffer == "Man"
                        || obj.wdesc._name.m_buffer == "Nothing"
                        || obj.wdesc._name.m_buffer.Contains("Lever")
                        || obj.wdesc._name.m_buffer.Contains("Gateway")
                        || obj.wdesc._name.m_buffer.Contains("Gate Stone")
                        || obj.wdesc._name.m_buffer.Contains("Target")
                        || obj.wdesc._name.m_buffer.Contains("Backpack")
                        || obj.wdesc._name.m_buffer.Contains("Odd Looking Vine")
                        || obj.wdesc._name.m_buffer.Contains("Pumpkin Vine")
                        || obj.wdesc._name.m_buffer.Contains("Font")
                        || obj.wdesc._name.m_buffer.Contains("Lair")
                        || obj.wdesc._name.m_buffer.Contains("Essence")
                        || obj.wdesc._name.m_buffer.Contains("Smelting")
                        || obj.wdesc._name.m_buffer.Contains("Documents")
                        || obj.wdesc._name.m_buffer.Contains("Harmonic Transference Field")
                        || obj.wdesc._name.m_buffer.Contains("Deeper into")
                        || obj.wdesc._name.m_buffer.Contains("Up to the")
                        || obj.wdesc._name.m_buffer.Contains("Pool")
                        )
                    {

                    }
                    else if (obj.wdesc._name.m_buffer.Contains("Exploration Marker"))
                    {

                    }
                    else if (obj.wdesc._name.m_buffer.Contains("Mysterious Hatch"))
                    {

                    }
                    else if (obj.wdesc._name.m_buffer.Contains("Cow") && !obj.wdesc._name.m_buffer.Contains("Auroch") && !obj.wdesc._name.m_buffer.Contains("Snowman"))
                    {

                    }
                    //else if (parsed.wdesc._name.m_buffer.Contains("Auroch"))
                    //{
                    //    weeniefileToPutItIn = "CreaturesAurochs";
                    //    weenieType = WeenieType.Cow_WeenieType;
                    //    addWeenie = true;
                    //}
                    else if (obj.wdesc._wcid >= 14342 && obj.wdesc._wcid <= 14347
                        || obj.wdesc._wcid >= 14404 && obj.wdesc._wcid <= 14409
                        )
                    {

                    }
                    else
                    {

                    }
                }
                else if (obj.wdesc._type == ITEM_TYPE.TYPE_GAMEBOARD)
                {

                }
                else if (obj.object_id < 0x80000000)
                {

                }
                else if (obj.wdesc._type == ITEM_TYPE.TYPE_ARMOR)
                {
                    weeniesTypeTemplate.TryGetValue((uint)WeenieType.Clothing_WeenieType, out template);
                }
                else if (obj.wdesc._type == ITEM_TYPE.TYPE_MELEE_WEAPON)
                {
                    weeniesTypeTemplate.TryGetValue((uint)WeenieType.MeleeWeapon_WeenieType, out template);
                }
                else if (obj.wdesc._type == ITEM_TYPE.TYPE_CLOTHING)
                {
                    weeniesTypeTemplate.TryGetValue((uint)WeenieType.Clothing_WeenieType, out template);
                }
                else if (obj.wdesc._type == ITEM_TYPE.TYPE_JEWELRY)
                {
                    weeniesTypeTemplate.TryGetValue((uint)WeenieType.Clothing_WeenieType, out template);
                }
                else if (obj.wdesc._type == ITEM_TYPE.TYPE_FOOD)
                {
                    weeniesTypeTemplate.TryGetValue((uint)WeenieType.Food_WeenieType, out template);
                }
                else if (obj.wdesc._type == ITEM_TYPE.TYPE_MONEY)
                {
                    weeniesTypeTemplate.TryGetValue((uint)WeenieType.Coin_WeenieType, out template);
                }
                else if (obj.wdesc._type == ITEM_TYPE.TYPE_MISSILE_WEAPON)
                {
                    weeniesTypeTemplate.TryGetValue((uint)WeenieType.MissileLauncher_WeenieType, out template);
                }
                else if (obj.wdesc._type == ITEM_TYPE.TYPE_GEM)
                {
                    weeniesTypeTemplate.TryGetValue((uint)WeenieType.Gem_WeenieType, out template);
                }
                else if (obj.wdesc._type == ITEM_TYPE.TYPE_SPELL_COMPONENTS)
                {
                    weeniesTypeTemplate.TryGetValue((uint)WeenieType.SpellComponent_WeenieType, out template);
                }
                else if (obj.wdesc._type == ITEM_TYPE.TYPE_KEY)
                {
                    weeniesTypeTemplate.TryGetValue((uint)WeenieType.Key_WeenieType, out template);
                }
                else if (obj.wdesc._type == ITEM_TYPE.TYPE_CASTER)
                {
                    weeniesTypeTemplate.TryGetValue((uint)WeenieType.Caster_WeenieType, out template);
                }
                else if (obj.wdesc._type == ITEM_TYPE.TYPE_MANASTONE)
                {
                    weeniesTypeTemplate.TryGetValue((uint)WeenieType.ManaStone_WeenieType, out template);
                }
                else if (obj.wdesc._type == ITEM_TYPE.TYPE_PROMISSORY_NOTE)
                {
                    weeniesTypeTemplate.TryGetValue((uint)WeenieType.Generic_WeenieType, out template);
                }
                else if (obj.wdesc._type == ITEM_TYPE.TYPE_CRAFT_ALCHEMY_BASE)
                {
                    weeniesTypeTemplate.TryGetValue((uint)WeenieType.Generic_WeenieType, out template);
                }
                else if (obj.wdesc._type == ITEM_TYPE.TYPE_CRAFT_ALCHEMY_INTERMEDIATE)
                {
                    weeniesTypeTemplate.TryGetValue((uint)WeenieType.Generic_WeenieType, out template);
                }
                else if (obj.wdesc._type == ITEM_TYPE.TYPE_CRAFT_COOKING_BASE)
                {
                    weeniesTypeTemplate.TryGetValue((uint)WeenieType.Generic_WeenieType, out template);
                }
                else if (obj.wdesc._type == ITEM_TYPE.TYPE_CRAFT_FLETCHING_BASE)
                {
                    weeniesTypeTemplate.TryGetValue((uint)WeenieType.Generic_WeenieType, out template);
                }
                else if (obj.wdesc._type == ITEM_TYPE.TYPE_CRAFT_FLETCHING_INTERMEDIATE)
                {
                    weeniesTypeTemplate.TryGetValue((uint)WeenieType.Generic_WeenieType, out template);
                }
                else if (obj.wdesc._type == ITEM_TYPE.TYPE_TINKERING_TOOL)
                {
                    weeniesTypeTemplate.TryGetValue((uint)WeenieType.CraftTool_WeenieType, out template);
                }
                else if (obj.wdesc._type == ITEM_TYPE.TYPE_TINKERING_MATERIAL)
                {
                    weeniesTypeTemplate.TryGetValue((uint)WeenieType.Generic_WeenieType, out template);
                }
                //else if (parsed.wdesc._type == ITEM_TYPE.TYPE_USELESS)
                //{
                //    weeniefileToPutItIn = "UselessItems";
                //    addWeenie = true;
                //}
                else if (((uint)obj.wdesc._type & (uint)ITEM_TYPE.TYPE_ITEM) > 0
                    )
                {
                    weeniesTypeTemplate.TryGetValue((uint)WeenieType.Generic_WeenieType, out template);
                }
                else if (obj.wdesc._name.m_buffer.Contains("Light")
                    || obj.wdesc._name.m_buffer.Contains("Lantern")
                    || obj.wdesc._name.m_buffer.Contains("Candelabra")
                    || obj.wdesc._name.m_buffer.Contains("Stove")
                    || obj.wdesc._name.m_buffer.Contains("Flame")
                    || obj.wdesc._name.m_buffer.Contains("Lamp")
                    || obj.wdesc._name.m_buffer.Contains("Chandelier")
                    || obj.wdesc._name.m_buffer.Contains("Torch")
                    || obj.wdesc._name.m_buffer.Contains("Hearth")
                    )
                {
                    weeniesTypeTemplate.TryGetValue((uint)WeenieType.LightSource_WeenieType, out template);
                }
                else
                {
                    weeniesTypeTemplate.TryGetValue((uint)WeenieType.Generic_WeenieType, out template);
                }

                if (template != null)
                {
                    obj.physicsdesc = template.physicsdesc;
                    obj.objdesc = template.objdesc;
                }
                else
                {
                    totalExceptions++;
                    System.Diagnostics.Debug.WriteLine($"Unable to regenerate object {obj.wdesc._name.m_buffer} ({obj.wdesc._wcid}), No template available for {obj.wdesc._type}.");
                    return null;
                }

                return obj;
            }
            catch (Exception ex)
            {
                totalExceptions++;               
                return null;
            }
        }

        private void WriteAppendedWeenieVendorObjectData(Dictionary<string, Dictionary<uint, CM_Vendor.gmVendorUI>> vendorObjects, string outputFolder)
        {
            string staticFolder = Path.Combine(outputFolder, "1-weenies");

            //string sqlCommand = "INSERT";
            string sqlCommand = "REPLACE";

            //if (!Directory.Exists(staticFolder))
            //    Directory.CreateDirectory(staticFolder);

            //Dictionary<string, int> fileCount = new Dictionary<string, int>();

            foreach (string key in vendorObjects.Keys)
            {
                foreach (var vendor in vendorObjects[key].Values)
                {

                    if (vendor.shopVendorID > 65535)
                        continue;

                    try
                    {
                        string keyFolder = Path.Combine(staticFolder, key);

                        string fullFile = Path.Combine(keyFolder, $"{vendor.shopVendorID}.sql");

                        //if (!fileCount.ContainsKey(key))
                        //    fileCount.Add(key, 0);

                        //string fullFile = Path.Combine(staticFolder, $"{key}_{fileCount[key]}.sql");

                        //if (File.Exists(fullFile))
                        //{
                        //    FileInfo fi = new FileInfo(fullFile);

                        //    // go to the next file if it's bigger than a MB
                        //    if (fi.Length > ((1048576) * 40))
                        //    {
                        //        fileCount[key]++;
                        //        fullFile = Path.Combine(staticFolder, $"{key}_{fileCount[key]}.sql");

                        //        if (File.Exists(fullFile))
                        //            File.Delete(fullFile);
                        //    }
                        //}

                        using (FileStream fs = new FileStream(fullFile, FileMode.Append))
                        {
                            using (StreamWriter writer = new StreamWriter(fs))
                            {
                                string header = $"/* Extended Vendor Data */" + Environment.NewLine;
                                writer.WriteLine(header);

                                string intsLine = "", bigintsLine = "", floatsLine = "", boolsLine = "", strsLine = "", didsLine = "", iidsLine = "";

                                intsLine += $"     , ({vendor.shopVendorID}, {(uint)STypeInt.MERCHANDISE_ITEM_TYPES_INT}, {(uint)vendor.shopVendorProfile.item_types}) /* {Enum.GetName(typeof(STypeInt), STypeInt.MERCHANDISE_ITEM_TYPES_INT)} */" + Environment.NewLine;
                                intsLine += $"     , ({vendor.shopVendorID}, {(uint)STypeInt.MERCHANDISE_MIN_VALUE_INT}, {(uint)vendor.shopVendorProfile.min_value}) /* {Enum.GetName(typeof(STypeInt), STypeInt.MERCHANDISE_MIN_VALUE_INT)} */" + Environment.NewLine;
                                intsLine += $"     , ({vendor.shopVendorID}, {(uint)STypeInt.MERCHANDISE_MAX_VALUE_INT}, {(uint)vendor.shopVendorProfile.max_value}) /* {Enum.GetName(typeof(STypeInt), STypeInt.MERCHANDISE_MAX_VALUE_INT)} */" + Environment.NewLine;

                                if (vendor.shopVendorProfile.magic == 1)
                                    boolsLine += $"     , ({vendor.shopVendorID}, {(uint)STypeBool.DEAL_MAGICAL_ITEMS_BOOL}, {true}) /* {Enum.GetName(typeof(STypeBool), STypeBool.DEAL_MAGICAL_ITEMS_BOOL)} */" + Environment.NewLine;

                                floatsLine += $"     , ({vendor.shopVendorID}, {(uint)STypeFloat.BUY_PRICE_FLOAT}, {(float)vendor.shopVendorProfile.buy_price}) /* {Enum.GetName(typeof(STypeFloat), STypeFloat.BUY_PRICE_FLOAT)} */" + Environment.NewLine;
                                floatsLine += $"     , ({vendor.shopVendorID}, {(uint)STypeFloat.SELL_PRICE_FLOAT}, {(float)vendor.shopVendorProfile.sell_price}) /* {Enum.GetName(typeof(STypeFloat), STypeFloat.SELL_PRICE_FLOAT)} */" + Environment.NewLine;


                                if (strsLine != "")
                                {
                                    strsLine = $"{sqlCommand} INTO `ace_object_properties_string` (`aceObjectId`, `strPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + strsLine.TrimStart("     ,".ToCharArray());
                                    strsLine = strsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(strsLine);
                                }
                                if (didsLine != "")
                                {
                                    didsLine = $"{sqlCommand} INTO `ace_object_properties_did` (`aceObjectId`, `didPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + didsLine.TrimStart("     ,".ToCharArray());
                                    didsLine = didsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(didsLine);
                                }
                                if (iidsLine != "")
                                {
                                    iidsLine = $"{sqlCommand} INTO `ace_object_properties_iid` (`aceObjectId`, `iidPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + iidsLine.TrimStart("     ,".ToCharArray());
                                    iidsLine = iidsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(iidsLine);
                                }
                                if (intsLine != "")
                                {
                                    intsLine = $"{sqlCommand} INTO `ace_object_properties_int` (`aceObjectId`, `intPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + intsLine.TrimStart("     ,".ToCharArray());
                                    intsLine = intsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(intsLine);
                                }
                                if (bigintsLine != "")
                                {
                                    bigintsLine = $"{sqlCommand} INTO `ace_object_properties_bigint` (`aceObjectId`, `bigIntPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + bigintsLine.TrimStart("     ,".ToCharArray());
                                    bigintsLine = bigintsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(bigintsLine);
                                }
                                if (floatsLine != "")
                                {
                                    floatsLine = $"{sqlCommand} INTO `ace_object_properties_double` (`aceObjectId`, `dblPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + floatsLine.TrimStart("     ,".ToCharArray());
                                    floatsLine = floatsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(floatsLine);
                                }
                                if (boolsLine != "")
                                {
                                    boolsLine = $"{sqlCommand} INTO `ace_object_properties_bool` (`aceObjectId`, `boolPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + boolsLine.TrimStart("     ,".ToCharArray());
                                    boolsLine = boolsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(boolsLine);
                                }


                                // Do something with the list of wcids vendor can sell here

                                //int pageNum = 0;
                                //foreach (var page in book.pageData)
                                //{
                                //    if (page.textIncluded == 1)
                                //    {
                                //        string pagesLine = "";

                                //        pagesLine += $"     , ({book.i_bookID}, {pageNum}, '{page.authorName.m_buffer?.Replace("'", "''")}', '{page.authorAccount.m_buffer?.Replace("'", "''")}', {page.authorID}, {page.ignoreAuthor}, '{page.pageText.m_buffer?.Replace("'", "''")}')" + Environment.NewLine;

                                //        if (pagesLine != "")
                                //        {
                                //            pagesLine = $"{sqlCommand} INTO `ace_object_properties_book` (`aceObjectId`, `page`, `authorName`, `authorAccount`, `authorId`, `ignoreAuthor`, `pageText`)" + Environment.NewLine
                                //                + "VALUES " + pagesLine.TrimStart("     ,".ToCharArray());
                                //            pagesLine = pagesLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                //            writer.WriteLine(pagesLine);
                                //        }
                                //    }
                                //    pageNum++;
                                //}
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("Unable to export object " + vendor.shopVendorID + ". Exception:" + Environment.NewLine + ex.ToString());
                    }
                }
            }
        }

        private void WriteWeenieVendorObjectData(Dictionary<string, Dictionary<uint, CM_Vendor.gmVendorUI>> vendorObjects, string outputFolder)
        {
            string staticFolder = Path.Combine(outputFolder, "5-weenievendordata");

            //string sqlCommand = "INSERT";
            string sqlCommand = "REPLACE";

            if (!Directory.Exists(staticFolder))
                Directory.CreateDirectory(staticFolder);

            Dictionary<string, int> fileCount = new Dictionary<string, int>();

            foreach (string key in vendorObjects.Keys)
            {
                foreach (var vendor in vendorObjects[key].Values)
                {

                    if (vendor.shopVendorID > 65535)
                        continue;

                    try
                    {
                        if (!fileCount.ContainsKey(key))
                            fileCount.Add(key, 0);

                        string fullFile = Path.Combine(staticFolder, $"{key}_{fileCount[key]}.sql");

                        if (File.Exists(fullFile))
                        {
                            FileInfo fi = new FileInfo(fullFile);

                            // go to the next file if it's bigger than a MB
                            if (fi.Length > ((1048576) * 40))
                            {
                                fileCount[key]++;
                                fullFile = Path.Combine(staticFolder, $"{key}_{fileCount[key]}.sql");

                                if (File.Exists(fullFile))
                                    File.Delete(fullFile);
                            }
                        }

                        using (FileStream fs = new FileStream(fullFile, FileMode.Append))
                        {
                            using (StreamWriter writer = new StreamWriter(fs))
                            {
                                string intsLine = "", bigintsLine = "", floatsLine = "", boolsLine = "", strsLine = "", didsLine = "", iidsLine = "";

                                intsLine += $"     , ({vendor.shopVendorID}, {(uint)STypeInt.MERCHANDISE_ITEM_TYPES_INT}, {(uint)vendor.shopVendorProfile.item_types})" + Environment.NewLine;
                                intsLine += $"     , ({vendor.shopVendorID}, {(uint)STypeInt.MERCHANDISE_MIN_VALUE_INT}, {(uint)vendor.shopVendorProfile.min_value})" + Environment.NewLine;
                                intsLine += $"     , ({vendor.shopVendorID}, {(uint)STypeInt.MERCHANDISE_MAX_VALUE_INT}, {(uint)vendor.shopVendorProfile.max_value})" + Environment.NewLine;

                                if (vendor.shopVendorProfile.magic == 1)
                                    boolsLine += $"     , ({vendor.shopVendorID}, {(uint)STypeBool.DEAL_MAGICAL_ITEMS_BOOL}, {true})" + Environment.NewLine;

                                floatsLine += $"     , ({vendor.shopVendorID}, {(uint)STypeFloat.BUY_PRICE_FLOAT}, {(float)vendor.shopVendorProfile.buy_price})" + Environment.NewLine;
                                floatsLine += $"     , ({vendor.shopVendorID}, {(uint)STypeFloat.SELL_PRICE_FLOAT}, {(float)vendor.shopVendorProfile.sell_price})" + Environment.NewLine;


                                if (strsLine != "")
                                {
                                    strsLine = $"{sqlCommand} INTO `ace_object_properties_string` (`aceObjectId`, `strPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + strsLine.TrimStart("     ,".ToCharArray());
                                    strsLine = strsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(strsLine);
                                }
                                if (didsLine != "")
                                {
                                    didsLine = $"{sqlCommand} INTO `ace_object_properties_did` (`aceObjectId`, `didPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + didsLine.TrimStart("     ,".ToCharArray());
                                    didsLine = didsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(didsLine);
                                }
                                if (iidsLine != "")
                                {
                                    iidsLine = $"{sqlCommand} INTO `ace_object_properties_iid` (`aceObjectId`, `iidPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + iidsLine.TrimStart("     ,".ToCharArray());
                                    iidsLine = iidsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(iidsLine);
                                }
                                if (intsLine != "")
                                {
                                    intsLine = $"{sqlCommand} INTO `ace_object_properties_int` (`aceObjectId`, `intPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + intsLine.TrimStart("     ,".ToCharArray());
                                    intsLine = intsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(intsLine);
                                }
                                if (bigintsLine != "")
                                {
                                    bigintsLine = $"{sqlCommand} INTO `ace_object_properties_bigint` (`aceObjectId`, `bigIntPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + bigintsLine.TrimStart("     ,".ToCharArray());
                                    bigintsLine = bigintsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(bigintsLine);
                                }
                                if (floatsLine != "")
                                {
                                    floatsLine = $"{sqlCommand} INTO `ace_object_properties_double` (`aceObjectId`, `dblPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + floatsLine.TrimStart("     ,".ToCharArray());
                                    floatsLine = floatsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(floatsLine);
                                }
                                if (boolsLine != "")
                                {
                                    boolsLine = $"{sqlCommand} INTO `ace_object_properties_bool` (`aceObjectId`, `boolPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + boolsLine.TrimStart("     ,".ToCharArray());
                                    boolsLine = boolsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(boolsLine);
                                }

                                
                                // Do something with the list of wcids vendor can sell here
                                
                                //int pageNum = 0;
                                //foreach (var page in book.pageData)
                                //{
                                //    if (page.textIncluded == 1)
                                //    {
                                //        string pagesLine = "";

                                //        pagesLine += $"     , ({book.i_bookID}, {pageNum}, '{page.authorName.m_buffer?.Replace("'", "''")}', '{page.authorAccount.m_buffer?.Replace("'", "''")}', {page.authorID}, {page.ignoreAuthor}, '{page.pageText.m_buffer?.Replace("'", "''")}')" + Environment.NewLine;

                                //        if (pagesLine != "")
                                //        {
                                //            pagesLine = $"{sqlCommand} INTO `ace_object_properties_book` (`aceObjectId`, `page`, `authorName`, `authorAccount`, `authorId`, `ignoreAuthor`, `pageText`)" + Environment.NewLine
                                //                + "VALUES " + pagesLine.TrimStart("     ,".ToCharArray());
                                //            pagesLine = pagesLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                //            writer.WriteLine(pagesLine);
                                //        }
                                //    }
                                //    pageNum++;
                                //}
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("Unable to export object " + vendor.shopVendorID + ". Exception:" + Environment.NewLine + ex.ToString());
                    }
                }
            }
        }

        private void WriteVendorObjectData(Dictionary<string, Dictionary<uint, CM_Vendor.gmVendorUI>> vendorObjects, string outputFolder)
        {
            string staticFolder = Path.Combine(outputFolder, "A-vendordata");

            //string sqlCommand = "INSERT";
            string sqlCommand = "REPLACE";

            if (!Directory.Exists(staticFolder))
                Directory.CreateDirectory(staticFolder);

            Dictionary<string, int> fileCount = new Dictionary<string, int>();

            foreach (string key in vendorObjects.Keys)
            {
                foreach (var vendor in vendorObjects[key].Values)
                {

                    if (vendor.shopVendorID < 65535)
                        continue;

                    try
                    {
                        if (!fileCount.ContainsKey(key))
                            fileCount.Add(key, 0);

                        string fullFile = Path.Combine(staticFolder, $"{key}_{fileCount[key]}.sql");

                        if (File.Exists(fullFile))
                        {
                            FileInfo fi = new FileInfo(fullFile);

                            // go to the next file if it's bigger than a MB
                            if (fi.Length > ((1048576) * 40))
                            {
                                fileCount[key]++;
                                fullFile = Path.Combine(staticFolder, $"{key}_{fileCount[key]}.sql");

                                if (File.Exists(fullFile))
                                    File.Delete(fullFile);
                            }
                        }

                        using (FileStream fs = new FileStream(fullFile, FileMode.Append))
                        {
                            using (StreamWriter writer = new StreamWriter(fs))
                            {
                                string intsLine = "", bigintsLine = "", floatsLine = "", boolsLine = "", strsLine = "", didsLine = "", iidsLine = "";

                                intsLine += $"     , ({vendor.shopVendorID}, {(uint)STypeInt.MERCHANDISE_ITEM_TYPES_INT}, {(uint)vendor.shopVendorProfile.item_types})" + Environment.NewLine;
                                intsLine += $"     , ({vendor.shopVendorID}, {(uint)STypeInt.MERCHANDISE_MIN_VALUE_INT}, {(uint)vendor.shopVendorProfile.min_value})" + Environment.NewLine;
                                intsLine += $"     , ({vendor.shopVendorID}, {(uint)STypeInt.MERCHANDISE_MAX_VALUE_INT}, {(uint)vendor.shopVendorProfile.max_value})" + Environment.NewLine;

                                if (vendor.shopVendorProfile.magic == 1)
                                    boolsLine += $"     , ({vendor.shopVendorID}, {(uint)STypeBool.DEAL_MAGICAL_ITEMS_BOOL}, {true})" + Environment.NewLine;

                                floatsLine += $"     , ({vendor.shopVendorID}, {(uint)STypeFloat.BUY_PRICE_FLOAT}, {(float)vendor.shopVendorProfile.buy_price})" + Environment.NewLine;
                                floatsLine += $"     , ({vendor.shopVendorID}, {(uint)STypeFloat.SELL_PRICE_FLOAT}, {(float)vendor.shopVendorProfile.sell_price})" + Environment.NewLine;


                                if (strsLine != "")
                                {
                                    strsLine = $"{sqlCommand} INTO `ace_object_properties_string` (`aceObjectId`, `strPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + strsLine.TrimStart("     ,".ToCharArray());
                                    strsLine = strsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(strsLine);
                                }
                                if (didsLine != "")
                                {
                                    didsLine = $"{sqlCommand} INTO `ace_object_properties_did` (`aceObjectId`, `didPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + didsLine.TrimStart("     ,".ToCharArray());
                                    didsLine = didsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(didsLine);
                                }
                                if (iidsLine != "")
                                {
                                    iidsLine = $"{sqlCommand} INTO `ace_object_properties_iid` (`aceObjectId`, `iidPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + iidsLine.TrimStart("     ,".ToCharArray());
                                    iidsLine = iidsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(iidsLine);
                                }
                                if (intsLine != "")
                                {
                                    intsLine = $"{sqlCommand} INTO `ace_object_properties_int` (`aceObjectId`, `intPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + intsLine.TrimStart("     ,".ToCharArray());
                                    intsLine = intsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(intsLine);
                                }
                                if (bigintsLine != "")
                                {
                                    bigintsLine = $"{sqlCommand} INTO `ace_object_properties_bigint` (`aceObjectId`, `bigIntPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + bigintsLine.TrimStart("     ,".ToCharArray());
                                    bigintsLine = bigintsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(bigintsLine);
                                }
                                if (floatsLine != "")
                                {
                                    floatsLine = $"{sqlCommand} INTO `ace_object_properties_double` (`aceObjectId`, `dblPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + floatsLine.TrimStart("     ,".ToCharArray());
                                    floatsLine = floatsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(floatsLine);
                                }
                                if (boolsLine != "")
                                {
                                    boolsLine = $"{sqlCommand} INTO `ace_object_properties_bool` (`aceObjectId`, `boolPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + boolsLine.TrimStart("     ,".ToCharArray());
                                    boolsLine = boolsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(boolsLine);
                                }


                                // Do something with the list of wcids vendor can sell here

                                //int pageNum = 0;
                                //foreach (var page in book.pageData)
                                //{
                                //    if (page.textIncluded == 1)
                                //    {
                                //        string pagesLine = "";

                                //        pagesLine += $"     , ({book.i_bookID}, {pageNum}, '{page.authorName.m_buffer?.Replace("'", "''")}', '{page.authorAccount.m_buffer?.Replace("'", "''")}', {page.authorID}, {page.ignoreAuthor}, '{page.pageText.m_buffer?.Replace("'", "''")}')" + Environment.NewLine;

                                //        if (pagesLine != "")
                                //        {
                                //            pagesLine = $"{sqlCommand} INTO `ace_object_properties_book` (`aceObjectId`, `page`, `authorName`, `authorAccount`, `authorId`, `ignoreAuthor`, `pageText`)" + Environment.NewLine
                                //                + "VALUES " + pagesLine.TrimStart("     ,".ToCharArray());
                                //            pagesLine = pagesLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                //            writer.WriteLine(pagesLine);
                                //        }
                                //    }
                                //    pageNum++;
                                //}
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("Unable to export object " + vendor.shopVendorID + ". Exception:" + Environment.NewLine + ex.ToString());
                    }
                }
            }
        }

        private void CreateBookObjectsList(CM_Writing.PageDataList parsed, List<uint> objectIds, Dictionary<string, List<CM_Physics.CreateObject>> staticObjects,
            List<uint> weenieIds, Dictionary<string, List<CM_Physics.CreateObject>> weenies, Dictionary<string, List<CM_Examine.SetAppraiseInfo>> appraisalObjects,
            List<uint> appraisalObjectIds, Dictionary<uint, string> appraisalObjectsCatagoryMap, Dictionary<uint, uint> appraisalObjectToWeenieId,
            //                           fileToPutItIn             bookid             pagedata
            List<uint> bookObjectIds, Dictionary<string, Dictionary<uint, CM_Writing.PageDataList>> bookObjects)
        {
            try
            {
                uint weenieId = 0;
                bool foundInObjectIds = false;
                bool foundInWeenieIds = false;
                foundInObjectIds = objectIds.Contains(parsed.i_bookID);
                appraisalObjectToWeenieId.TryGetValue(parsed.i_bookID, out weenieId);
                foundInWeenieIds = weenieIds.Contains(weenieId);

                if (!foundInObjectIds && !(weenieId > 0))
                    return;

                bool addIt = true;
                //bool addWeenie = false;
                string fileToPutItIn = "BookData";


                appraisalObjectsCatagoryMap.TryGetValue(parsed.i_bookID, out fileToPutItIn);

                if (fileToPutItIn == null)
                    fileToPutItIn = "0-BookData";

                if (!foundInObjectIds && weenieId > 0)
                {
                    if (!foundInWeenieIds)
                        return;

                    parsed.i_bookID = weenieId;
                }

                // de-dupe based on position and wcid
                if (addIt) //&& !PositionRecorded(parsed, processedWeeniePositions[parsed.wdesc._wcid], parsed.physicsdesc.pos, margin))
                {
                    if (!bookObjects.ContainsKey(fileToPutItIn))
                    {
                        bookObjects.Add(fileToPutItIn, new Dictionary<uint, CM_Writing.PageDataList>());
                    }
                    
                    if (bookObjectIds.Contains(parsed.i_bookID))
                    {
                        return;
                    }

                    bookObjects[fileToPutItIn].Add(parsed.i_bookID, parsed);
                    bookObjectIds.Add(parsed.i_bookID);

                    if (bookObjectIds.Contains(weenieId) && weenieId > 0)
                    {
                        if (!bookObjects[fileToPutItIn].Keys.Contains(parsed.i_bookID))
                        {
                            CM_Writing.PageDataList parsedClone;

                            parsedClone = new CM_Writing.PageDataList();
                            parsedClone.i_bookID = weenieId;

                            parsedClone.authorId = parsed.authorId;
                            parsedClone.authorName = parsed.authorName;
                            parsedClone.inscription = parsed.inscription;
                            parsedClone.i_maxNumPages = parsed.i_maxNumPages;
                            parsedClone.numPages = parsed.numPages;
                            parsedClone.pageData = parsed.pageData;

                            bookObjects[fileToPutItIn].Add(parsedClone.i_bookID, parsedClone);
                            bookObjectIds.Add(parsedClone.i_bookID);
                        }
                    }

                    if (!bookObjectIds.Contains(weenieId) && weenieId > 0)
                    {
                        CM_Writing.PageDataList parsedClone;

                        parsedClone = new CM_Writing.PageDataList();
                        parsedClone.i_bookID = weenieId;

                        parsedClone.authorId = parsed.authorId;
                        parsedClone.authorName = parsed.authorName;
                        parsedClone.inscription = parsed.inscription;
                        parsedClone.i_maxNumPages = parsed.i_maxNumPages;
                        parsedClone.numPages = parsed.numPages;
                        parsedClone.pageData = parsed.pageData;

                        if (!bookObjects.ContainsKey(fileToPutItIn))
                        {
                            bookObjects.Add(fileToPutItIn, new Dictionary<uint, CM_Writing.PageDataList>());
                        }

                        bookObjects[fileToPutItIn].Add(parsedClone.i_bookID, parsedClone);
                        bookObjectIds.Add(parsedClone.i_bookID);
                    }
                    totalHits++;
                }
            }
            catch (Exception ex)
            {
                totalExceptions++;
            }
        }

        private void WriteBookObjectData(Dictionary<string, Dictionary<uint, CM_Writing.PageDataList>> bookObjects, string outputFolder)
        {
            string staticFolder = Path.Combine(outputFolder, "8-bookdata");

            //string sqlCommand = "INSERT";
            string sqlCommand = "REPLACE";

            if (!Directory.Exists(staticFolder))
                Directory.CreateDirectory(staticFolder);

            Dictionary<string, int> fileCount = new Dictionary<string, int>();

            foreach (string key in bookObjects.Keys)
            {
                foreach (var book in bookObjects[key].Values)
                {

                    if (book.i_bookID < 65535)
                        continue;

                    try
                    {
                        if (!fileCount.ContainsKey(key))
                            fileCount.Add(key, 0);

                        string fullFile = Path.Combine(staticFolder, $"{key}_{fileCount[key]}.sql");

                        if (File.Exists(fullFile))
                        {
                            FileInfo fi = new FileInfo(fullFile);

                            // go to the next file if it's bigger than a MB
                            if (fi.Length > ((1048576) * 40))
                            {
                                fileCount[key]++;
                                fullFile = Path.Combine(staticFolder, $"{key}_{fileCount[key]}.sql");

                                if (File.Exists(fullFile))
                                    File.Delete(fullFile);
                            }
                        }

                        using (FileStream fs = new FileStream(fullFile, FileMode.Append))
                        {
                            using (StreamWriter writer = new StreamWriter(fs))
                            {
                                string intsLine = "", bigintsLine = "", floatsLine = "", boolsLine = "", strsLine = "", didsLine = "", iidsLine = "";

                                intsLine += $"     , ({book.i_bookID}, {(uint)STypeInt.APPRAISAL_PAGES_INT}, {(uint)book.numPages})" + Environment.NewLine;
                                intsLine += $"     , ({book.i_bookID}, {(uint)STypeInt.APPRAISAL_MAX_PAGES_INT}, {(uint)book.i_maxNumPages})" + Environment.NewLine;

                                intsLine += $"     , ({book.i_bookID}, {(uint)STypeInt.AVAILABLE_CHARACTER_INT}, {(uint)book.maxNumCharsPerPage})" + Environment.NewLine;

                                iidsLine += $"     , ({book.i_bookID}, {(uint)STypeIID.SCRIBE_IID}, {(uint)book.authorId})" + Environment.NewLine;
                                if (book.authorName.m_buffer != null)
                                    strsLine += $"     , ({book.i_bookID}, {(uint)STypeString.SCRIBE_NAME_STRING}, '{book.authorName.m_buffer.Replace("'", "''")}')" + Environment.NewLine;
                                if (book.inscription.m_buffer != null)
                                    strsLine += $"     , ({book.i_bookID}, {(uint)STypeString.INSCRIPTION_STRING}, '{book.inscription.m_buffer.Replace("'", "''")}')" + Environment.NewLine;

                                if (strsLine != "")
                                {
                                    strsLine = $"{sqlCommand} INTO `ace_object_properties_string` (`aceObjectId`, `strPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + strsLine.TrimStart("     ,".ToCharArray());
                                    strsLine = strsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(strsLine);
                                }
                                if (didsLine != "")
                                {
                                    didsLine = $"{sqlCommand} INTO `ace_object_properties_did` (`aceObjectId`, `didPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + didsLine.TrimStart("     ,".ToCharArray());
                                    didsLine = didsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(didsLine);
                                }
                                if (iidsLine != "")
                                {
                                    iidsLine = $"{sqlCommand} INTO `ace_object_properties_iid` (`aceObjectId`, `iidPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + iidsLine.TrimStart("     ,".ToCharArray());
                                    iidsLine = iidsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(iidsLine);
                                }
                                if (intsLine != "")
                                {
                                    intsLine = $"{sqlCommand} INTO `ace_object_properties_int` (`aceObjectId`, `intPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + intsLine.TrimStart("     ,".ToCharArray());
                                    intsLine = intsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(intsLine);
                                }
                                if (bigintsLine != "")
                                {
                                    bigintsLine = $"{sqlCommand} INTO `ace_object_properties_bigint` (`aceObjectId`, `bigIntPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + bigintsLine.TrimStart("     ,".ToCharArray());
                                    bigintsLine = bigintsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(bigintsLine);
                                }
                                if (floatsLine != "")
                                {
                                    floatsLine = $"{sqlCommand} INTO `ace_object_properties_double` (`aceObjectId`, `dblPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + floatsLine.TrimStart("     ,".ToCharArray());
                                    floatsLine = floatsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(floatsLine);
                                }
                                if (boolsLine != "")
                                {
                                    boolsLine = $"{sqlCommand} INTO `ace_object_properties_bool` (`aceObjectId`, `boolPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + boolsLine.TrimStart("     ,".ToCharArray());
                                    boolsLine = boolsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(boolsLine);
                                }

                                int pageNum = 0;
                                foreach (var page in book.pageData)
                                {
                                    if (page.textIncluded == 1)
                                    {
                                        string pagesLine = "";

                                        pagesLine += $"     , ({book.i_bookID}, {pageNum}, '{page.authorName.m_buffer?.Replace("'", "''")}', '{page.authorAccount.m_buffer?.Replace("'", "''")}', {page.authorID}, {page.ignoreAuthor}, '{page.pageText.m_buffer?.Replace("'", "''")}')" + Environment.NewLine;

                                        if (pagesLine != "")
                                        {
                                            pagesLine = $"{sqlCommand} INTO `ace_object_properties_book` (`aceObjectId`, `page`, `authorName`, `authorAccount`, `authorId`, `ignoreAuthor`, `pageText`)" + Environment.NewLine
                                                + "VALUES " + pagesLine.TrimStart("     ,".ToCharArray());
                                            pagesLine = pagesLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                            writer.WriteLine(pagesLine);
                                        }
                                    }
                                    pageNum++;
                                }
                            }
                            //}
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("Unable to export object " + book + ". Exception:" + Environment.NewLine + ex.ToString());
                    }
                }
            }
        }

        private void WriteAppendedWeenieBookObjectData(Dictionary<string, Dictionary<uint, CM_Writing.PageDataList>> bookObjects, string outputFolder)
        {
            string staticFolder = Path.Combine(outputFolder, "1-weenies");

            //string sqlCommand = "INSERT";
            string sqlCommand = "REPLACE";

            //if (!Directory.Exists(staticFolder))
            //    Directory.CreateDirectory(staticFolder);

            //Dictionary<string, int> fileCount = new Dictionary<string, int>();

            foreach (string key in bookObjects.Keys)
            {
                foreach (var book in bookObjects[key].Values)
                {

                    if (book.i_bookID > 65535)
                        continue;

                    try
                    {
                        string keyFolder = Path.Combine(staticFolder, key);

                        string fullFile = Path.Combine(keyFolder, $"{book.i_bookID}.sql");

                        //if (!fileCount.ContainsKey(key))
                        //    fileCount.Add(key, 0);

                        //string fullFile = Path.Combine(staticFolder, $"{key}_{fileCount[key]}.sql");

                        //if (File.Exists(fullFile))
                        //{
                        //    FileInfo fi = new FileInfo(fullFile);

                        //    // go to the next file if it's bigger than a MB
                        //    if (fi.Length > ((1048576) * 40))
                        //    {
                        //        fileCount[key]++;
                        //        fullFile = Path.Combine(staticFolder, $"{key}_{fileCount[key]}.sql");

                        //        if (File.Exists(fullFile))
                        //            File.Delete(fullFile);
                        //    }
                        //}

                        //foreach (var page in bookObjects[key].Values)
                        //{
                        using (FileStream fs = new FileStream(fullFile, FileMode.Append))
                        {
                            using (StreamWriter writer = new StreamWriter(fs))
                            {
                                string header = $"/* Extended Book Data */" + Environment.NewLine;
                                writer.WriteLine(header);

                                string intsLine = "", bigintsLine = "", floatsLine = "", boolsLine = "", strsLine = "", didsLine = "", iidsLine = "";

                                intsLine += $"     , ({book.i_bookID}, {(uint)STypeInt.APPRAISAL_PAGES_INT}, {(uint)book.numPages}) /* {Enum.GetName(typeof(STypeInt), STypeInt.APPRAISAL_PAGES_INT)} */" + Environment.NewLine;
                                intsLine += $"     , ({book.i_bookID}, {(uint)STypeInt.APPRAISAL_MAX_PAGES_INT}, {(uint)book.i_maxNumPages}) /* {Enum.GetName(typeof(STypeInt), STypeInt.APPRAISAL_MAX_PAGES_INT)} */" + Environment.NewLine;

                                intsLine += $"     , ({book.i_bookID}, {(uint)STypeInt.AVAILABLE_CHARACTER_INT}, {(uint)book.maxNumCharsPerPage}) /* {Enum.GetName(typeof(STypeInt), STypeInt.AVAILABLE_CHARACTER_INT)} */" + Environment.NewLine;

                                //if (book.authorId > 0)
                                //    iidsLine += $"     , ({book.i_bookID}, {(uint)STypeIID.SCRIBE_IID}, {(uint)book.authorId})" + Environment.NewLine;
                                if (book.authorName.m_buffer != null)
                                    strsLine += $"     , ({book.i_bookID}, {(uint)STypeString.SCRIBE_NAME_STRING}, '{book.authorName.m_buffer.Replace("'", "''")}') /* {Enum.GetName(typeof(STypeString), STypeString.SCRIBE_NAME_STRING)} */" + Environment.NewLine;
                                //if (book.inscription.m_buffer != null)
                                //    strsLine += $"     , ({book.i_bookID}, {(uint)STypeString.INSCRIPTION_STRING}, '{book.inscription.m_buffer.Replace("'", "''")}')" + Environment.NewLine;

                                if (strsLine != "")
                                {
                                    strsLine = $"{sqlCommand} INTO `ace_object_properties_string` (`aceObjectId`, `strPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + strsLine.TrimStart("     ,".ToCharArray());
                                    strsLine = strsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(strsLine);
                                }
                                if (didsLine != "")
                                {
                                    didsLine = $"{sqlCommand} INTO `ace_object_properties_did` (`aceObjectId`, `didPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + didsLine.TrimStart("     ,".ToCharArray());
                                    didsLine = didsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(didsLine);
                                }
                                if (iidsLine != "")
                                {
                                    iidsLine = $"{sqlCommand} INTO `ace_object_properties_iid` (`aceObjectId`, `iidPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + iidsLine.TrimStart("     ,".ToCharArray());
                                    iidsLine = iidsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(iidsLine);
                                }
                                if (intsLine != "")
                                {
                                    intsLine = $"{sqlCommand} INTO `ace_object_properties_int` (`aceObjectId`, `intPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + intsLine.TrimStart("     ,".ToCharArray());
                                    intsLine = intsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(intsLine);
                                }
                                if (bigintsLine != "")
                                {
                                    bigintsLine = $"{sqlCommand} INTO `ace_object_properties_bigint` (`aceObjectId`, `bigIntPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + bigintsLine.TrimStart("     ,".ToCharArray());
                                    bigintsLine = bigintsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(bigintsLine);
                                }
                                if (floatsLine != "")
                                {
                                    floatsLine = $"{sqlCommand} INTO `ace_object_properties_double` (`aceObjectId`, `dblPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + floatsLine.TrimStart("     ,".ToCharArray());
                                    floatsLine = floatsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(floatsLine);
                                }
                                if (boolsLine != "")
                                {
                                    boolsLine = $"{sqlCommand} INTO `ace_object_properties_bool` (`aceObjectId`, `boolPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + boolsLine.TrimStart("     ,".ToCharArray());
                                    boolsLine = boolsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(boolsLine);
                                }

                                int pageNum = 0;
                                foreach (var page in book.pageData)
                                {
                                    if (page.textIncluded == 1)
                                    {
                                        string pagesLine = "";

                                        if (page.authorAccount.m_buffer == "Password is cheese" || page.authorAccount.m_buffer == "beer good")
                                            page.authorAccount.m_buffer = "prewritten";

                                        pagesLine += $"     , ({book.i_bookID}, {pageNum}, '{page.authorName.m_buffer?.Replace("'", "''")}', '{page.authorAccount.m_buffer?.Replace("'", "''")}', {page.authorID}, {page.ignoreAuthor}, '{page.pageText.m_buffer?.Replace("'", "''")}')" + Environment.NewLine;

                                        if (pagesLine != "")
                                        {
                                            pagesLine = $"{sqlCommand} INTO `ace_object_properties_book` (`aceObjectId`, `page`, `authorName`, `authorAccount`, `authorId`, `ignoreAuthor`, `pageText`)" + Environment.NewLine
                                                + "VALUES " + pagesLine.TrimStart("     ,".ToCharArray());
                                            pagesLine = pagesLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                            writer.WriteLine(pagesLine);
                                        }
                                    }
                                    pageNum++;
                                }
                            }
                            //}
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("Unable to export object " + book + ". Exception:" + Environment.NewLine + ex.ToString());
                    }
                }
            }
        }

        private void WriteWeenieBookObjectData(Dictionary<string, Dictionary<uint, CM_Writing.PageDataList>> bookObjects, string outputFolder)
        {
            string staticFolder = Path.Combine(outputFolder, "3-weeniebookdata");

            //string sqlCommand = "INSERT";
            string sqlCommand = "REPLACE";

            if (!Directory.Exists(staticFolder))
                Directory.CreateDirectory(staticFolder);

            Dictionary<string, int> fileCount = new Dictionary<string, int>();

            foreach (string key in bookObjects.Keys)
            {
                foreach (var book in bookObjects[key].Values)
                {

                    if (book.i_bookID > 65535)
                        continue;

                    try
                    {
                        if (!fileCount.ContainsKey(key))
                            fileCount.Add(key, 0);

                        string fullFile = Path.Combine(staticFolder, $"{key}_{fileCount[key]}.sql");

                        if (File.Exists(fullFile))
                        {
                            FileInfo fi = new FileInfo(fullFile);

                            // go to the next file if it's bigger than a MB
                            if (fi.Length > ((1048576) * 40))
                            {
                                fileCount[key]++;
                                fullFile = Path.Combine(staticFolder, $"{key}_{fileCount[key]}.sql");

                                if (File.Exists(fullFile))
                                    File.Delete(fullFile);
                            }
                        }

                        //foreach (var page in bookObjects[key].Values)
                        //{
                        using (FileStream fs = new FileStream(fullFile, FileMode.Append))
                        {
                            using (StreamWriter writer = new StreamWriter(fs))
                            {
                                string intsLine = "", bigintsLine = "", floatsLine = "", boolsLine = "", strsLine = "", didsLine = "", iidsLine = "";

                                intsLine += $"     , ({book.i_bookID}, {(uint)STypeInt.APPRAISAL_PAGES_INT}, {(uint)book.numPages})" + Environment.NewLine;
                                intsLine += $"     , ({book.i_bookID}, {(uint)STypeInt.APPRAISAL_MAX_PAGES_INT}, {(uint)book.i_maxNumPages})" + Environment.NewLine;

                                intsLine += $"     , ({book.i_bookID}, {(uint)STypeInt.AVAILABLE_CHARACTER_INT}, {(uint)book.maxNumCharsPerPage})" + Environment.NewLine;

                                //if (book.authorId > 0)
                                //    iidsLine += $"     , ({book.i_bookID}, {(uint)STypeIID.SCRIBE_IID}, {(uint)book.authorId})" + Environment.NewLine;
                                if (book.authorName.m_buffer != null)
                                    strsLine += $"     , ({book.i_bookID}, {(uint)STypeString.SCRIBE_NAME_STRING}, '{book.authorName.m_buffer.Replace("'", "''")}')" + Environment.NewLine;
                                if (book.inscription.m_buffer != null)
                                    strsLine += $"     , ({book.i_bookID}, {(uint)STypeString.INSCRIPTION_STRING}, '{book.inscription.m_buffer.Replace("'", "''")}')" + Environment.NewLine;

                                if (strsLine != "")
                                {
                                    strsLine = $"{sqlCommand} INTO `ace_object_properties_string` (`aceObjectId`, `strPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + strsLine.TrimStart("     ,".ToCharArray());
                                    strsLine = strsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(strsLine);
                                }
                                if (didsLine != "")
                                {
                                    didsLine = $"{sqlCommand} INTO `ace_object_properties_did` (`aceObjectId`, `didPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + didsLine.TrimStart("     ,".ToCharArray());
                                    didsLine = didsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(didsLine);
                                }
                                if (iidsLine != "")
                                {
                                    iidsLine = $"{sqlCommand} INTO `ace_object_properties_iid` (`aceObjectId`, `iidPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + iidsLine.TrimStart("     ,".ToCharArray());
                                    iidsLine = iidsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(iidsLine);
                                }
                                if (intsLine != "")
                                {
                                    intsLine = $"{sqlCommand} INTO `ace_object_properties_int` (`aceObjectId`, `intPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + intsLine.TrimStart("     ,".ToCharArray());
                                    intsLine = intsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(intsLine);
                                }
                                if (bigintsLine != "")
                                {
                                    bigintsLine = $"{sqlCommand} INTO `ace_object_properties_bigint` (`aceObjectId`, `bigIntPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + bigintsLine.TrimStart("     ,".ToCharArray());
                                    bigintsLine = bigintsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(bigintsLine);
                                }
                                if (floatsLine != "")
                                {
                                    floatsLine = $"{sqlCommand} INTO `ace_object_properties_double` (`aceObjectId`, `dblPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + floatsLine.TrimStart("     ,".ToCharArray());
                                    floatsLine = floatsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(floatsLine);
                                }
                                if (boolsLine != "")
                                {
                                    boolsLine = $"{sqlCommand} INTO `ace_object_properties_bool` (`aceObjectId`, `boolPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + boolsLine.TrimStart("     ,".ToCharArray());
                                    boolsLine = boolsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(boolsLine);
                                }

                                int pageNum = 0;
                                foreach (var page in book.pageData)
                                {
                                    if (page.textIncluded == 1)
                                    {
                                        string pagesLine = "";

                                        pagesLine += $"     , ({book.i_bookID}, {pageNum}, '{page.authorName.m_buffer?.Replace("'", "''")}', '{page.authorAccount.m_buffer?.Replace("'", "''")}', {page.authorID}, {page.ignoreAuthor}, '{page.pageText.m_buffer?.Replace("'", "''")}')" + Environment.NewLine;

                                        if (pagesLine != "")
                                        {
                                            pagesLine = $"{sqlCommand} INTO `ace_object_properties_book` (`aceObjectId`, `page`, `authorName`, `authorAccount`, `authorId`, `ignoreAuthor`, `pageText`)" + Environment.NewLine
                                                + "VALUES " + pagesLine.TrimStart("     ,".ToCharArray());
                                            pagesLine = pagesLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                            writer.WriteLine(pagesLine);
                                        }
                                    }
                                    pageNum++;
                                }
                            }
                            //}
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("Unable to export object " + book + ". Exception:" + Environment.NewLine + ex.ToString());
                    }
                }
            }
        }

        private void CreatePageObjectsList(CM_Writing.PageData parsed, List<uint> objectIds, Dictionary<string, List<CM_Physics.CreateObject>> staticObjects, 
            List<uint> weenieIds, Dictionary<string, List<CM_Physics.CreateObject>> weenies, Dictionary<string, List<CM_Examine.SetAppraiseInfo>> appraisalObjects, 
            List<uint> appraisalObjectIds, Dictionary<uint, string> appraisalObjectsCatagoryMap, Dictionary<uint, uint> appraisalObjectToWeenieId,
            //                                   fileToPutItIn      bookid           pageid         pagedata
            List<uint> bookObjectIds, Dictionary<string, Dictionary<uint, Dictionary<uint, CM_Writing.PageData>>> pageObjects)
        {
            try
            {
                uint weenieId = 0;
                bool foundInObjectIds = false;
                bool foundInWeenieIds = false;
                foundInObjectIds = objectIds.Contains(parsed.bookID);
                appraisalObjectToWeenieId.TryGetValue(parsed.bookID, out weenieId);
                foundInWeenieIds = weenieIds.Contains(weenieId);

                if (!foundInObjectIds && !(weenieId > 0))
                    return;

                bool addIt = true;
                //bool addWeenie = false;
                string fileToPutItIn = "PageData";


                appraisalObjectsCatagoryMap.TryGetValue(parsed.bookID, out fileToPutItIn);

                if (fileToPutItIn == null)
                    fileToPutItIn = "0-PageData";

                if (!foundInObjectIds && weenieId > 0)
                {
                    if (!foundInWeenieIds)
                        return;

                    parsed.bookID = weenieId;
                }

                // de-dupe based on position and wcid
                if (addIt) //&& !PositionRecorded(parsed, processedWeeniePositions[parsed.wdesc._wcid], parsed.physicsdesc.pos, margin))
                {
                    if (!pageObjects.ContainsKey(fileToPutItIn))
                    {
                        pageObjects.Add(fileToPutItIn, new Dictionary<uint, Dictionary<uint, CM_Writing.PageData>>());
                    }

                    if (!pageObjects[fileToPutItIn].ContainsKey(parsed.bookID))
                    {
                        pageObjects[fileToPutItIn].Add(parsed.bookID, new Dictionary<uint, CM_Writing.PageData>());
                    }

                    if (bookObjectIds.Contains(parsed.bookID))
                    {
                        if (pageObjects[fileToPutItIn][parsed.bookID].ContainsKey(parsed.page))
                            return;
                    }

                    pageObjects[fileToPutItIn][parsed.bookID].Add(parsed.page, parsed);
                    bookObjectIds.Add(parsed.bookID);

                    if (bookObjectIds.Contains(weenieId) && weenieId > 0)
                    {
                        if (!pageObjects[fileToPutItIn][weenieId].Keys.Contains(parsed.page))
                        {
                            CM_Writing.PageData parsedClone;

                            parsedClone = new CM_Writing.PageData();
                            parsedClone.bookID = weenieId;

                            parsedClone.authorAccount = parsed.authorAccount;
                            parsedClone.authorID = parsed.authorID;
                            parsedClone.authorName = parsed.authorName;
                            parsedClone.flags = parsed.flags;
                            parsedClone.ignoreAuthor = parsed.ignoreAuthor;
                            parsedClone.page = parsed.page;
                            parsedClone.pageText = parsed.pageText;
                            parsedClone.textIncluded = parsed.textIncluded;

                            pageObjects[fileToPutItIn][parsedClone.bookID].Add(parsedClone.page, parsedClone);
                            bookObjectIds.Add(parsedClone.bookID);
                        }
                    }

                    if (!bookObjectIds.Contains(weenieId) && weenieId > 0)
                    {
                        CM_Writing.PageData parsedClone;

                        parsedClone = new CM_Writing.PageData();
                        parsedClone.bookID = weenieId;

                        parsedClone.authorAccount = parsed.authorAccount;
                        parsedClone.authorID = parsed.authorID;
                        parsedClone.authorName = parsed.authorName;
                        parsedClone.flags = parsed.flags;
                        parsedClone.ignoreAuthor = parsed.ignoreAuthor;
                        parsedClone.page = parsed.page;
                        parsedClone.pageText = parsed.pageText;
                        parsedClone.textIncluded = parsed.textIncluded;

                        if (!pageObjects.ContainsKey(fileToPutItIn))
                        {
                            pageObjects.Add(fileToPutItIn, new Dictionary<uint, Dictionary<uint, CM_Writing.PageData>>());
                        }

                        if (!pageObjects[fileToPutItIn].ContainsKey(parsedClone.bookID))
                        {
                            pageObjects[fileToPutItIn].Add(parsedClone.bookID, new Dictionary<uint, CM_Writing.PageData>());
                        }

                        pageObjects[fileToPutItIn][parsedClone.bookID].Add(parsedClone.page, parsedClone);
                        bookObjectIds.Add(parsedClone.bookID);
                    }
                    totalHits++;
                }
            }
            catch (Exception ex)
            {
                totalExceptions++;
            }
        }

        private void WritePageObjectData(Dictionary<string, Dictionary<uint, Dictionary<uint, CM_Writing.PageData>>> pageObjects, string outputFolder)
        {
            string staticFolder = Path.Combine(outputFolder, "9-pagedata");

            //string sqlCommand = "INSERT";
            string sqlCommand = "REPLACE";

            if (!Directory.Exists(staticFolder))
                Directory.CreateDirectory(staticFolder);

            Dictionary<string, int> fileCount = new Dictionary<string, int>();

            foreach (string key in pageObjects.Keys)
            {
                foreach (var book in pageObjects[key].Keys)
                {

                    if (book < 65535)
                        continue;

                    try
                    {
                        if (!fileCount.ContainsKey(key))
                            fileCount.Add(key, 0);

                        string fullFile = Path.Combine(staticFolder, $"{key}_{fileCount[key]}.sql");

                        if (File.Exists(fullFile))
                        {
                            FileInfo fi = new FileInfo(fullFile);

                            // go to the next file if it's bigger than a MB
                            if (fi.Length > ((1048576) * 40))
                            {
                                fileCount[key]++;
                                fullFile = Path.Combine(staticFolder, $"{key}_{fileCount[key]}.sql");

                                if (File.Exists(fullFile))
                                    File.Delete(fullFile);
                            }
                        }

                        foreach (var page in pageObjects[key][book].Values)
                        {
                            using (FileStream fs = new FileStream(fullFile, FileMode.Append))
                            {
                                using (StreamWriter writer = new StreamWriter(fs))
                                {
                                    string pagesLine = "";

                                    pagesLine += $"     , ({page.bookID}, {page.page}, '{page.authorName.m_buffer?.Replace("'", "''")}', '{page.authorAccount.m_buffer?.Replace("'", "''")}', {page.authorID}, {page.ignoreAuthor}, '{page.pageText.m_buffer?.Replace("'", "''")}')" + Environment.NewLine;

                                    if (pagesLine != "")
                                    {
                                        pagesLine = $"{sqlCommand} INTO `ace_object_properties_book` (`aceObjectId`, `page`, `authorName`, `authorAccount`, `authorId`, `ignoreAuthor`, `pageText`)" + Environment.NewLine
                                            + "VALUES " + pagesLine.TrimStart("     ,".ToCharArray());
                                        pagesLine = pagesLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                        writer.WriteLine(pagesLine);
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("Unable to export object " + book + ". Exception:" + Environment.NewLine + ex.ToString());
                    }
                }
            }
        }

        private void WriteAppendedWeeniePageObjectData(Dictionary<string, Dictionary<uint, Dictionary<uint, CM_Writing.PageData>>> pageObjects, string outputFolder)
        {
            string staticFolder = Path.Combine(outputFolder, "1-weenies");

            //string sqlCommand = "INSERT";
            string sqlCommand = "REPLACE";

            //if (!Directory.Exists(staticFolder))
            //    Directory.CreateDirectory(staticFolder);

            //Dictionary<string, int> fileCount = new Dictionary<string, int>();

            foreach (string key in pageObjects.Keys)
            {
                foreach (var book in pageObjects[key].Keys)
                {

                    if (book > 65535)
                        continue;

                    try
                    {
                        string keyFolder = Path.Combine(staticFolder, key);

                        string fullFile = Path.Combine(keyFolder, $"{book}.sql");

                        //if (!fileCount.ContainsKey(key))
                        //    fileCount.Add(key, 0);

                        //string fullFile = Path.Combine(staticFolder, $"{key}_{fileCount[key]}.sql");

                        //if (File.Exists(fullFile))
                        //{
                        //    FileInfo fi = new FileInfo(fullFile);

                        //    // go to the next file if it's bigger than a MB
                        //    if (fi.Length > ((1048576) * 40))
                        //    {
                        //        fileCount[key]++;
                        //        fullFile = Path.Combine(staticFolder, $"{key}_{fileCount[key]}.sql");

                        //        if (File.Exists(fullFile))
                        //            File.Delete(fullFile);
                        //    }
                        //}

                        foreach (var page in pageObjects[key][book].Values)
                        {
                            using (FileStream fs = new FileStream(fullFile, FileMode.Append))
                            {
                                using (StreamWriter writer = new StreamWriter(fs))
                                {
                                    string header = $"/* Extended Page Data */" + Environment.NewLine;
                                    writer.WriteLine(header);

                                    string pagesLine = "";

                                    if (page.authorAccount.m_buffer == "Password is cheese" || page.authorAccount.m_buffer == "beer good")
                                        page.authorAccount.m_buffer = "prewritten";

                                    pagesLine += $"     , ({page.bookID}, {page.page}, '{page.authorName.m_buffer?.Replace("'", "''")}', '{page.authorAccount.m_buffer?.Replace("'", "''")}', {page.authorID}, {page.ignoreAuthor}, '{page.pageText.m_buffer?.Replace("'", "''")}')" + Environment.NewLine;

                                    if (pagesLine != "")
                                    {
                                        pagesLine = $"{sqlCommand} INTO `ace_object_properties_book` (`aceObjectId`, `page`, `authorName`, `authorAccount`, `authorId`, `ignoreAuthor`, `pageText`)" + Environment.NewLine
                                            + "VALUES " + pagesLine.TrimStart("     ,".ToCharArray());
                                        pagesLine = pagesLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                        writer.WriteLine(pagesLine);
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("Unable to export object " + book + ". Exception:" + Environment.NewLine + ex.ToString());
                    }
                }
            }
        }

        private void WriteWeeniePageObjectData(Dictionary<string, Dictionary<uint, Dictionary<uint, CM_Writing.PageData>>> pageObjects, string outputFolder)
        {
            string staticFolder = Path.Combine(outputFolder, "4-weeniepagedata");

            //string sqlCommand = "INSERT";
            string sqlCommand = "REPLACE";

            if (!Directory.Exists(staticFolder))
                Directory.CreateDirectory(staticFolder);

            Dictionary<string, int> fileCount = new Dictionary<string, int>();

            foreach (string key in pageObjects.Keys)
            {
                foreach (var book in pageObjects[key].Keys)
                {

                    if (book > 65535)
                        continue;

                    try
                    {
                        if (!fileCount.ContainsKey(key))
                            fileCount.Add(key, 0);

                        string fullFile = Path.Combine(staticFolder, $"{key}_{fileCount[key]}.sql");

                        if (File.Exists(fullFile))
                        {
                            FileInfo fi = new FileInfo(fullFile);

                            // go to the next file if it's bigger than a MB
                            if (fi.Length > ((1048576) * 40))
                            {
                                fileCount[key]++;
                                fullFile = Path.Combine(staticFolder, $"{key}_{fileCount[key]}.sql");

                                if (File.Exists(fullFile))
                                    File.Delete(fullFile);
                            }
                        }

                        foreach (var page in pageObjects[key][book].Values)
                        {
                            using (FileStream fs = new FileStream(fullFile, FileMode.Append))
                            {
                                using (StreamWriter writer = new StreamWriter(fs))
                                {
                                    string pagesLine = "";

                                    pagesLine += $"     , ({page.bookID}, {page.page}, '{page.authorName.m_buffer?.Replace("'", "''")}', '{page.authorAccount.m_buffer?.Replace("'", "''")}', {page.authorID}, {page.ignoreAuthor}, '{page.pageText.m_buffer?.Replace("'", "''")}')" + Environment.NewLine;

                                    if (pagesLine != "")
                                    {
                                        pagesLine = $"{sqlCommand} INTO `ace_object_properties_book` (`aceObjectId`, `page`, `authorName`, `authorAccount`, `authorId`, `ignoreAuthor`, `pageText`)" + Environment.NewLine
                                            + "VALUES " + pagesLine.TrimStart("     ,".ToCharArray());
                                        pagesLine = pagesLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                        writer.WriteLine(pagesLine);
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("Unable to export object " + book + ". Exception:" + Environment.NewLine + ex.ToString());
                    }
                }
            }
        }

        private void CreateAppraisalObjectsList(CM_Examine.SetAppraiseInfo parsed, List<uint> objectIds, Dictionary<string, List<CM_Physics.CreateObject>> staticObjects, List<uint> weenieIds, Dictionary<string, List<CM_Physics.CreateObject>> weenies, Dictionary<string, List<CM_Examine.SetAppraiseInfo>> appraisalObjects, List<uint> appraisalObjectIds, Dictionary<uint, string> appraisalObjectsCatagoryMap, Dictionary<uint, uint> appraisalObjectToWeenieId)
        {
            try
            {
                uint weenieId = 0;
                bool foundInObjectIds = false;
                bool foundInWeenieIds = false;
                foundInObjectIds = objectIds.Contains(parsed.i_objid);
                appraisalObjectToWeenieId.TryGetValue(parsed.i_objid, out weenieId);
                foundInWeenieIds = weenieIds.Contains(weenieId);

                if (!foundInObjectIds && !(weenieId > 0))
                    return;

                bool addIt = true;
                //bool addWeenie = false;
                string fileToPutItIn = "AssessmentData";
                //string weeniefileToPutItIn = "OtherWeenies";
                //float margin = 0.02f;

                appraisalObjectsCatagoryMap.TryGetValue(parsed.i_objid, out fileToPutItIn);

                if (fileToPutItIn == null)
                    fileToPutItIn = "0-AssessmentData";

                if (!foundInObjectIds && weenieId > 0)
                {
                    if (!foundInWeenieIds)
                        return;

                    // parsed.i_objid = weenieId;
                }

                // de-dupe based on position and wcid
                if (addIt) //&& !PositionRecorded(parsed, processedWeeniePositions[parsed.wdesc._wcid], parsed.physicsdesc.pos, margin))
                {
                    if (!appraisalObjects.ContainsKey(fileToPutItIn))
                        appraisalObjects.Add(fileToPutItIn, new List<CM_Examine.SetAppraiseInfo>());

                    if (appraisalObjectIds.Contains(parsed.i_objid))
                    {
                        int i = 0;
                        for (int ListIndex = 0; ListIndex < appraisalObjects[fileToPutItIn].Count; ListIndex++)
                        {
                            if (appraisalObjects[fileToPutItIn][ListIndex].i_objid == parsed.i_objid)
                            {
                                i = ListIndex;
                                break;
                            }
                        }
                        if (appraisalObjects[fileToPutItIn][i].i_prof.success_flag == 0)
                        {
                            appraisalObjects[fileToPutItIn].RemoveAt(i);
                            appraisalObjectIds.Remove(parsed.i_objid);
                        }
                        else
                            return;
                    }

                    appraisalObjects[fileToPutItIn].Add(parsed);
                    appraisalObjectIds.Add(parsed.i_objid);

                    if (!appraisalObjectIds.Contains(weenieId) && weenieId > 0)
                    {
                        CM_Examine.SetAppraiseInfo parsedClone;

                        parsedClone = new CM_Examine.SetAppraiseInfo();
                        parsedClone.i_objid = weenieId;
                        parsedClone.i_prof = parsed.i_prof;

                        //parsedClone.i_objid = weenieId;
                        if (appraisalObjectIds.Contains(parsedClone.i_objid))
                        {
                            int i = 0;
                            for (int ListIndex = 0; ListIndex < appraisalObjects[fileToPutItIn].Count; ListIndex++)
                            {
                                if (appraisalObjects[fileToPutItIn][ListIndex].i_objid == parsedClone.i_objid)
                                {
                                    i = ListIndex;
                                    break;
                                }
                            }
                            if (appraisalObjects[fileToPutItIn][i].i_prof.success_flag == 0)
                            {
                                appraisalObjects[fileToPutItIn].RemoveAt(i);
                                appraisalObjectIds.Remove(parsedClone.i_objid);
                            }
                            else
                                return;
                        }

                        appraisalObjects[fileToPutItIn].Add(parsedClone);
                        appraisalObjectIds.Add(parsedClone.i_objid);
                    }
                    totalHits++;
                }
            }
            catch (Exception ex)
            {
                totalExceptions++;
            }
        }

        private void CreateWeenieAppraisalObjectsList(CM_Examine.SetAppraiseInfo parsed, List<uint> objectIds, Dictionary<string, List<CM_Physics.CreateObject>> staticObjects, List<uint> weenieIds, Dictionary<string, List<CM_Physics.CreateObject>> weenies, Dictionary<string, List<CM_Examine.SetAppraiseInfo>> appraisalObjects, List<uint> appraisalObjectIds, Dictionary<uint, string> appraisalObjectsCatagoryMap, Dictionary<uint, uint> appraisalObjectToWeenieId)
        {
            try
            {
                uint weenieId = 0;
                bool foundInObjectIds = false;
                bool foundInWeenieIds = false;
                foundInObjectIds = objectIds.Contains(parsed.i_objid);
                appraisalObjectToWeenieId.TryGetValue(parsed.i_objid, out weenieId);
                foundInWeenieIds = weenieIds.Contains(weenieId);

                if (!foundInObjectIds && !(weenieId > 0))
                    return;

                bool addIt = true;
                //bool addWeenie = false;
                string fileToPutItIn = "AssessmentData";


                appraisalObjectsCatagoryMap.TryGetValue(parsed.i_objid, out fileToPutItIn);

                if (fileToPutItIn == null)
                    fileToPutItIn = "0-AssessmentData";

                if (!foundInObjectIds && weenieId > 0)
                {
                    if (!foundInWeenieIds)
                        return;

                    parsed.i_objid = weenieId;
                }

                // de-dupe based on position and wcid
                if (addIt) //&& !PositionRecorded(parsed, processedWeeniePositions[parsed.wdesc._wcid], parsed.physicsdesc.pos, margin))
                {

                    if (parsed.i_objid > 65535)
                        return;

                    if (!appraisalObjects.ContainsKey(fileToPutItIn))
                        appraisalObjects.Add(fileToPutItIn, new List<CM_Examine.SetAppraiseInfo>());

                    if (appraisalObjectIds.Contains(parsed.i_objid))
                    {
                        int i = 0;
                        for (int ListIndex = 0; ListIndex < appraisalObjects[fileToPutItIn].Count; ListIndex++)
                        {
                            if (appraisalObjects[fileToPutItIn][ListIndex].i_objid == parsed.i_objid)
                            {
                                i = ListIndex;
                                break;
                            }
                        }
                        if (appraisalObjects[fileToPutItIn][i].i_prof.success_flag == 0)
                        {

                            appraisalObjects[fileToPutItIn].RemoveAt(i);
                            appraisalObjectIds.Remove(parsed.i_objid);
                        }
                        else
                            return;
                    }

                    appraisalObjects[fileToPutItIn].Add(parsed);
                    appraisalObjectIds.Add(parsed.i_objid);

                    if (!appraisalObjectIds.Contains(weenieId) && weenieId > 0)
                    {
                        CM_Examine.SetAppraiseInfo parsedClone;

                        parsedClone = new CM_Examine.SetAppraiseInfo();
                        parsedClone.i_objid = weenieId;
                        parsedClone.i_prof = parsed.i_prof;

                        parsedClone.i_objid = weenieId;
                        appraisalObjects[fileToPutItIn].Add(parsedClone);
                        appraisalObjectIds.Add(parsedClone.i_objid);
                    }
                    totalHits++;
                }
            }
            catch (Exception ex)
            {
                totalExceptions++;
            }
        }

        private void WriteAppraisalObjectData(Dictionary<string, List<CM_Examine.SetAppraiseInfo>> appraisalObjects, string outputFolder)
        {
            string staticFolder = Path.Combine(outputFolder, "7-apprasialobjects");

            //string sqlCommand = "INSERT";
            string sqlCommand = "REPLACE";

            if (!Directory.Exists(staticFolder))
                Directory.CreateDirectory(staticFolder);

            Dictionary<string, int> fileCount = new Dictionary<string, int>();

            foreach (string key in appraisalObjects.Keys)
            {
                foreach (var parsed in appraisalObjects[key])
                {

                    if (parsed.i_objid < 65535)
                        continue;

                    try
                    {
                        if (!fileCount.ContainsKey(key))
                            fileCount.Add(key, 0);

                        string fullFile = Path.Combine(staticFolder, $"{key}_{fileCount[key]}.sql");

                        if (File.Exists(fullFile))
                        {
                            FileInfo fi = new FileInfo(fullFile);

                            // go to the next file if it's bigger than a MB
                            if (fi.Length > ((1048576) * 40))
                            {
                                fileCount[key]++;
                                fullFile = Path.Combine(staticFolder, $"{key}_{fileCount[key]}.sql");

                                if (File.Exists(fullFile))
                                    File.Delete(fullFile);
                            }
                        }

                        using (FileStream fs = new FileStream(fullFile, FileMode.Append))
                        {
                            using (StreamWriter writer = new StreamWriter(fs))
                            {

                                if (parsed.i_objid < 65535)
                                    continue;

                                string intsLine = "", bigintsLine = "", floatsLine = "", boolsLine = "", strsLine = "", didsLine = "", iidsLine = "";
                                string skillsLine = "", attributesLine = "", attribute2ndsLine = "", bodyDamageValuesLine = "", bodyDamageVariancesLine = "", bodyArmorValuesLine = "", numsLine = "";
                                string spellsLine = ""; //, bigintsLine = "", floatsLine = "", boolsLine = "", strsLine = "", didsLine = "", iidsLine = "";

                                if (parsed.i_prof._strStatsTable.hashTable.Count > 0)
                                {
                                    foreach (var stat in parsed.i_prof._strStatsTable.hashTable)
                                    {
                                        strsLine += $"     , ({parsed.i_objid}, {(uint)stat.Key}, '{stat.Value.m_buffer?.Replace("'", "''")}')" + Environment.NewLine;
                                    }
                                }

                                if (parsed.i_prof._didStatsTable.hashTable.Count > 0)
                                {
                                    foreach (var stat in parsed.i_prof._didStatsTable.hashTable)
                                    {
                                        didsLine += $"     , ({parsed.i_objid}, {(uint)stat.Key}, {(uint)stat.Value})" + Environment.NewLine;
                                    }
                                }

                                if (parsed.i_prof._intStatsTable.hashTable.Count > 0)
                                {
                                    foreach (var stat in parsed.i_prof._intStatsTable.hashTable)
                                    {
                                        intsLine += $"     , ({parsed.i_objid}, {(uint)stat.Key}, {(uint)stat.Value})" + Environment.NewLine;
                                    }
                                }

                                if (parsed.i_prof._int64StatsTable.hashTable.Count > 0)
                                {
                                    foreach (var stat in parsed.i_prof._int64StatsTable.hashTable)
                                    {
                                        bigintsLine += $"     , ({parsed.i_objid}, {(uint)stat.Key}, {(uint)stat.Value})" + Environment.NewLine;
                                    }
                                }

                                if (parsed.i_prof._floatStatsTable.hashTable.Count > 0)
                                {
                                    foreach (var stat in parsed.i_prof._floatStatsTable.hashTable)
                                    {
                                        if (float.IsInfinity((float)stat.Value))
                                            floatsLine += $"     , ({parsed.i_objid}, {(uint)stat.Key}, {(float)Convert.ToDouble(stat.Value.ToString().Substring(0, 5))})" + Environment.NewLine;
                                        else
                                            floatsLine += $"     , ({parsed.i_objid}, {(uint)stat.Key}, {(float)stat.Value})" + Environment.NewLine;
                                    }
                                }

                                if (parsed.i_prof._boolStatsTable.hashTable.Count > 0)
                                {
                                    foreach (var stat in parsed.i_prof._boolStatsTable.hashTable)
                                    {
                                        boolsLine += $"     , ({parsed.i_objid}, {(uint)stat.Key}, {(uint)stat.Value})" + Environment.NewLine;
                                    }
                                }

                                if (parsed.i_prof._spellsTable.list.Count > 0)
                                {
                                    foreach (var stat in parsed.i_prof._spellsTable.list)
                                    {
                                        if (Enum.IsDefined(typeof(SpellID), stat))
                                            spellsLine += $"     , ({parsed.i_objid}, {(uint)stat})" + Environment.NewLine;
                                    }
                                }

                                if ((parsed.i_prof.header & (uint)CM_Examine.AppraisalProfile.AppraisalProfilePackHeader.Packed_WeaponProfile) != 0)
                                {
                                    intsLine += $"     , ({parsed.i_objid}, {(uint)STypeInt.DAMAGE_TYPE_INT}, {(uint)parsed.i_prof._weaponProfileTable._damage_type})" + Environment.NewLine;
                                    intsLine += $"     , ({parsed.i_objid}, {(uint)STypeInt.WEAPON_TIME_INT}, {(uint)parsed.i_prof._weaponProfileTable._weapon_time})" + Environment.NewLine;
                                    intsLine += $"     , ({parsed.i_objid}, {(uint)STypeInt.WEAPON_SKILL_INT}, {(uint)parsed.i_prof._weaponProfileTable._weapon_skill})" + Environment.NewLine;
                                    intsLine += $"     , ({parsed.i_objid}, {(uint)STypeInt.DAMAGE_INT}, {(uint)parsed.i_prof._weaponProfileTable._weapon_damage})" + Environment.NewLine;
                                    floatsLine += $"     , ({parsed.i_objid}, {(uint)STypeFloat.DAMAGE_VARIANCE_FLOAT}, {(float)parsed.i_prof._weaponProfileTable._damage_variance})" + Environment.NewLine;
                                    floatsLine += $"     , ({parsed.i_objid}, {(uint)STypeFloat.DAMAGE_MOD_FLOAT}, {(float)parsed.i_prof._weaponProfileTable._damage_mod})" + Environment.NewLine;
                                    floatsLine += $"     , ({parsed.i_objid}, {(uint)STypeFloat.WEAPON_LENGTH_FLOAT}, {(float)parsed.i_prof._weaponProfileTable._weapon_length})" + Environment.NewLine;
                                    floatsLine += $"     , ({parsed.i_objid}, {(uint)STypeFloat.MAXIMUM_VELOCITY_FLOAT}, {(float)parsed.i_prof._weaponProfileTable._max_velocity})" + Environment.NewLine;
                                    floatsLine += $"     , ({parsed.i_objid}, {(uint)STypeFloat.WEAPON_OFFENSE_FLOAT}, {(float)parsed.i_prof._weaponProfileTable._weapon_offense})" + Environment.NewLine;
                                    //intsLine += $"     , ({parsed.i_objid}, {(uint)STypeInt.???}, {(uint)parsed.i_prof._weaponProfileTable._max_velocity_estimated})" + Environment.NewLine;
                                }

                                if ((parsed.i_prof.header & (uint)CM_Examine.AppraisalProfile.AppraisalProfilePackHeader.Packed_HookProfile) != 0)
                                {
                                    intsLine += $"     , ({parsed.i_objid}, {(uint)STypeInt.LOCATIONS_INT}, {(uint)parsed.i_prof._hookProfileTable._validLocations})" + Environment.NewLine;
                                    intsLine += $"     , ({parsed.i_objid}, {(uint)STypeInt.AMMO_TYPE_INT}, {(uint)parsed.i_prof._hookProfileTable._ammoType})" + Environment.NewLine;
                                    boolsLine += $"     , ({parsed.i_objid}, {(uint)STypeBool.INSCRIBABLE_BOOL}, {parsed.i_prof._hookProfileTable.isInscribable})" + Environment.NewLine;
                                    //boolsLine += $"     , ({parsed.i_objid}, {(uint)STypeBool.???}, {parsed.i_prof._hookProfileTable.isHealer})" + Environment.NewLine;
                                    //boolsLine += $"     , ({parsed.i_objid}, {(uint)STypeBool.???}, {parsed.i_prof._hookProfileTable.isLockpick})" + Environment.NewLine;
                                }

                                if ((parsed.i_prof.header & (uint)CM_Examine.AppraisalProfile.AppraisalProfilePackHeader.Packed_ArmorProfile) != 0)
                                {
                                    floatsLine += $"     , ({parsed.i_objid}, {(uint)STypeFloat.ARMOR_MOD_VS_SLASH_FLOAT}, {(float)parsed.i_prof._armorProfileTable._mod_vs_slash})" + Environment.NewLine;
                                    floatsLine += $"     , ({parsed.i_objid}, {(uint)STypeFloat.ARMOR_MOD_VS_PIERCE_FLOAT}, {(float)parsed.i_prof._armorProfileTable._mod_vs_pierce})" + Environment.NewLine;
                                    floatsLine += $"     , ({parsed.i_objid}, {(uint)STypeFloat.ARMOR_MOD_VS_BLUDGEON_FLOAT}, {(float)parsed.i_prof._armorProfileTable._mod_vs_bludgeon})" + Environment.NewLine;
                                    floatsLine += $"     , ({parsed.i_objid}, {(uint)STypeFloat.ARMOR_MOD_VS_COLD_FLOAT}, {(float)parsed.i_prof._armorProfileTable._mod_vs_cold})" + Environment.NewLine;
                                    floatsLine += $"     , ({parsed.i_objid}, {(uint)STypeFloat.ARMOR_MOD_VS_FIRE_FLOAT}, {(float)parsed.i_prof._armorProfileTable._mod_vs_fire})" + Environment.NewLine;
                                    floatsLine += $"     , ({parsed.i_objid}, {(uint)STypeFloat.ARMOR_MOD_VS_ACID_FLOAT}, {(float)parsed.i_prof._armorProfileTable._mod_vs_acid})" + Environment.NewLine;
                                    floatsLine += $"     , ({parsed.i_objid}, {(uint)STypeFloat.ARMOR_MOD_VS_ELECTRIC_FLOAT}, {(float)parsed.i_prof._armorProfileTable._mod_vs_electric})" + Environment.NewLine;
                                    floatsLine += $"     , ({parsed.i_objid}, {(uint)STypeFloat.ARMOR_MOD_VS_NETHER_FLOAT}, {(float)parsed.i_prof._armorProfileTable._mod_vs_nether})" + Environment.NewLine;
                                }

                                if ((parsed.i_prof.header & (uint)CM_Examine.AppraisalProfile.AppraisalProfilePackHeader.Packed_CreatureProfile) != 0)
                                {
                                    if (parsed.i_prof.success_flag == 0)
                                    {
                                        ////attribute2ndsLine += $"     , ({parsed.i_objid}, {(uint)STypeAttribute2nd.HEALTH_ATTRIBUTE_2ND}, {(uint)parsed.i_prof._creatureProfileTable._health})" + Environment.NewLine;
                                        attribute2ndsLine += $"     , ({parsed.i_objid}, {(uint)STypeAttribute2nd.MAX_HEALTH_ATTRIBUTE_2ND}, {(uint)parsed.i_prof._creatureProfileTable._max_health})" + Environment.NewLine;

                                        ////attribute2ndsLine += $"     , ({parsed.i_objid}, {(uint)STypeAttribute2nd.HEALTH_ATTRIBUTE_2ND}, {(uint)parsed.i_prof._creatureProfileTable._health})" + Environment.NewLine;
                                        ////attribute2ndsLine += $"     , ({parsed.i_objid}, {(uint)64}, {(uint)parsed.i_prof._creatureProfileTable._max_health})" + Environment.NewLine;
                                    }
                                    else
                                    {
                                        attributesLine += $"     , ({parsed.i_objid}, {(uint)STypeAttribute.STRENGTH_ATTRIBUTE}, {(uint)parsed.i_prof._creatureProfileTable._strength})" + Environment.NewLine;
                                        attributesLine += $"     , ({parsed.i_objid}, {(uint)STypeAttribute.ENDURANCE_ATTRIBUTE}, {(uint)parsed.i_prof._creatureProfileTable._endurance})" + Environment.NewLine;
                                        attributesLine += $"     , ({parsed.i_objid}, {(uint)STypeAttribute.COORDINATION_ATTRIBUTE}, {(uint)parsed.i_prof._creatureProfileTable._coordination})" + Environment.NewLine;
                                        attributesLine += $"     , ({parsed.i_objid}, {(uint)STypeAttribute.QUICKNESS_ATTRIBUTE}, {(uint)parsed.i_prof._creatureProfileTable._quickness})" + Environment.NewLine;
                                        attributesLine += $"     , ({parsed.i_objid}, {(uint)STypeAttribute.FOCUS_ATTRIBUTE}, {(uint)parsed.i_prof._creatureProfileTable._focus})" + Environment.NewLine;
                                        attributesLine += $"     , ({parsed.i_objid}, {(uint)STypeAttribute.SELF_ATTRIBUTE}, {(uint)parsed.i_prof._creatureProfileTable._self})" + Environment.NewLine;


                                        ////attribute2ndsLine += $"     , ({parsed.i_objid}, {(uint)STypeAttribute2nd.HEALTH_ATTRIBUTE_2ND}, {(uint)parsed.i_prof._creatureProfileTable._health})" + Environment.NewLine;
                                        ////attribute2ndsLine += $"     , ({parsed.i_objid}, {(uint)STypeAttribute2nd.STAMINA_ATTRIBUTE_2ND}, {(uint)parsed.i_prof._creatureProfileTable._stamina})" + Environment.NewLine;
                                        ////attribute2ndsLine += $"     , ({parsed.i_objid}, {(uint)STypeAttribute2nd.MANA_ATTRIBUTE_2ND}, {(uint)parsed.i_prof._creatureProfileTable._mana})" + Environment.NewLine;

                                        attribute2ndsLine += $"     , ({parsed.i_objid}, {(uint)STypeAttribute2nd.MAX_HEALTH_ATTRIBUTE_2ND}, {(uint)parsed.i_prof._creatureProfileTable._max_health})" + Environment.NewLine;
                                        attribute2ndsLine += $"     , ({parsed.i_objid}, {(uint)STypeAttribute2nd.MAX_STAMINA_ATTRIBUTE_2ND}, {(uint)parsed.i_prof._creatureProfileTable._max_stamina})" + Environment.NewLine;
                                        attribute2ndsLine += $"     , ({parsed.i_objid}, {(uint)STypeAttribute2nd.MAX_MANA_ATTRIBUTE_2ND}, {(uint)parsed.i_prof._creatureProfileTable._max_mana})" + Environment.NewLine;


                                        ////attributesLine += $"     , ({parsed.i_objid}, {(uint)1}, {(uint)parsed.i_prof._creatureProfileTable._strength})" + Environment.NewLine;
                                        ////attributesLine += $"     , ({parsed.i_objid}, {(uint)2}, {(uint)parsed.i_prof._creatureProfileTable._endurance})" + Environment.NewLine;
                                        ////attributesLine += $"     , ({parsed.i_objid}, {(uint)4}, {(uint)parsed.i_prof._creatureProfileTable._coordination})" + Environment.NewLine;
                                        ////attributesLine += $"     , ({parsed.i_objid}, {(uint)8}, {(uint)parsed.i_prof._creatureProfileTable._quickness})" + Environment.NewLine;
                                        ////attributesLine += $"     , ({parsed.i_objid}, {(uint)16}, {(uint)parsed.i_prof._creatureProfileTable._focus})" + Environment.NewLine;
                                        ////attributesLine += $"     , ({parsed.i_objid}, {(uint)32}, {(uint)parsed.i_prof._creatureProfileTable._self})" + Environment.NewLine;

                                        //////attribute2ndsLine += $"     , ({parsed.i_objid}, {(uint)64}, {(uint)parsed.i_prof._creatureProfileTable._health})" + Environment.NewLine;
                                        //////attribute2ndsLine += $"     , ({parsed.i_objid}, {(uint)128}, {(uint)parsed.i_prof._creatureProfileTable._stamina})" + Environment.NewLine;
                                        //////attribute2ndsLine += $"     , ({parsed.i_objid}, {(uint)256}, {(uint)parsed.i_prof._creatureProfileTable._mana})" + Environment.NewLine;

                                        ////attribute2ndsLine += $"     , ({parsed.i_objid}, {(uint)64}, {(uint)parsed.i_prof._creatureProfileTable._max_health})" + Environment.NewLine;
                                        ////attribute2ndsLine += $"     , ({parsed.i_objid}, {(uint)128}, {(uint)parsed.i_prof._creatureProfileTable._max_stamina})" + Environment.NewLine;
                                        ////attribute2ndsLine += $"     , ({parsed.i_objid}, {(uint)256}, {(uint)parsed.i_prof._creatureProfileTable._max_mana})" + Environment.NewLine;
                                    }
                                }

                                if (strsLine != "")
                                {
                                    strsLine = $"{sqlCommand} INTO `ace_object_properties_string` (`aceObjectId`, `strPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + strsLine.TrimStart("     ,".ToCharArray());
                                    strsLine = strsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(strsLine);
                                }
                                if (didsLine != "")
                                {
                                    didsLine = $"{sqlCommand} INTO `ace_object_properties_did` (`aceObjectId`, `didPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + didsLine.TrimStart("     ,".ToCharArray());
                                    didsLine = didsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(didsLine);
                                }
                                if (iidsLine != "")
                                {
                                    iidsLine = $"{sqlCommand} INTO `ace_object_properties_iid` (`aceObjectId`, `iidPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + iidsLine.TrimStart("     ,".ToCharArray());
                                    iidsLine = iidsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(iidsLine);
                                }
                                if (intsLine != "")
                                {
                                    intsLine = $"{sqlCommand} INTO `ace_object_properties_int` (`aceObjectId`, `intPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + intsLine.TrimStart("     ,".ToCharArray());
                                    intsLine = intsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(intsLine);
                                }
                                if (bigintsLine != "")
                                {
                                    bigintsLine = $"{sqlCommand} INTO `ace_object_properties_bigint` (`aceObjectId`, `bigIntPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + bigintsLine.TrimStart("     ,".ToCharArray());
                                    bigintsLine = bigintsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(bigintsLine);
                                }
                                if (floatsLine != "")
                                {
                                    floatsLine = $"{sqlCommand} INTO `ace_object_properties_double` (`aceObjectId`, `dblPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + floatsLine.TrimStart("     ,".ToCharArray());
                                    floatsLine = floatsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(floatsLine);
                                }
                                if (boolsLine != "")
                                {
                                    boolsLine = $"{sqlCommand} INTO `ace_object_properties_bool` (`aceObjectId`, `boolPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + boolsLine.TrimStart("     ,".ToCharArray());
                                    boolsLine = boolsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(boolsLine);
                                }
                                if (spellsLine != "")
                                {
                                    spellsLine = $"{sqlCommand} INTO `ace_object_properties_spell` (`aceObjectId`, `spellId`)" + Environment.NewLine
                                        + "VALUES " + spellsLine.TrimStart("     ,".ToCharArray());
                                    spellsLine = spellsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(spellsLine);
                                }
                                if (attributesLine != "")
                                {
                                    attributesLine = $"{sqlCommand} INTO `ace_object_properties_attribute` (`aceObjectId`, `attributeId`, `attributeBase`)" + Environment.NewLine
                                        + "VALUES " + attributesLine.TrimStart("     ,".ToCharArray());
                                    attributesLine = attributesLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(attributesLine);
                                }
                                if (attribute2ndsLine != "")
                                {
                                    attribute2ndsLine = $"{sqlCommand} INTO `ace_object_properties_attribute2nd` (`aceObjectId`, `attribute2ndId`, `attribute2ndValue`)" + Environment.NewLine
                                        + "VALUES " + attribute2ndsLine.TrimStart("     ,".ToCharArray());
                                    attribute2ndsLine = attribute2ndsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(attribute2ndsLine);
                                }

                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("Unable to export object " + parsed.i_objid + ". Exception:" + Environment.NewLine + ex.ToString());
                    }
                }
            }
        }

        private void WriteAppendedWeenieAppraisalObjectData(Dictionary<string, List<CM_Examine.SetAppraiseInfo>> appraisalObjects, string outputFolder)
        {
            string staticFolder = Path.Combine(outputFolder, "1-weenies");

            //string sqlCommand = "INSERT";
            string sqlCommand = "REPLACE";

            //if (!Directory.Exists(staticFolder))
            //    Directory.CreateDirectory(staticFolder);

            //Dictionary<string, int> fileCount = new Dictionary<string, int>();

            foreach (string key in appraisalObjects.Keys)
            {
                foreach (var parsed in appraisalObjects[key])
                {

                    if (parsed.i_objid > 65535)
                        continue;

                    try
                    {
                        //if (!fileCount.ContainsKey(key))
                        //    fileCount.Add(key, 0);

                        //string fullFile = Path.Combine(staticFolder, $"{key}_{fileCount[key]}.sql");

                        //if (File.Exists(fullFile))
                        //{
                        //    FileInfo fi = new FileInfo(fullFile);

                        //    // go to the next file if it's bigger than a MB
                        //    if (fi.Length > ((1048576) * 40))
                        //    {
                        //        fileCount[key]++;
                        //        fullFile = Path.Combine(staticFolder, $"{key}_{fileCount[key]}.sql");

                        //        if (File.Exists(fullFile))
                        //            File.Delete(fullFile);
                        //    }
                        //}

                        string keyFolder = Path.Combine(staticFolder, key);

                        string fullFile = Path.Combine(keyFolder, $"{parsed.i_objid}.sql");

                        using (FileStream fs = new FileStream(fullFile, FileMode.Append))
                        {
                            using (StreamWriter writer = new StreamWriter(fs))
                            {

                                string header = $"/* Extended Appraisal Data */" + Environment.NewLine;
                                writer.WriteLine(header);

                                //if (parsed.i_objid > 65535)
                                //    continue;

                                string intsLine = "", bigintsLine = "", floatsLine = "", boolsLine = "", strsLine = "", didsLine = "", iidsLine = "";
                                string skillsLine = "", attributesLine = "", attribute2ndsLine = "", bodyDamageValuesLine = "", bodyDamageVariancesLine = "", bodyArmorValuesLine = "", numsLine = "";
                                string spellsLine = ""; //, bigintsLine = "", floatsLine = "", boolsLine = "", strsLine = "", didsLine = "", iidsLine = "";

                                if (parsed.i_prof._strStatsTable.hashTable.Count > 0)
                                {
                                    foreach (var stat in parsed.i_prof._strStatsTable.hashTable)
                                    {
                                        if (
                                            (uint)stat.Key == (uint)STypeString.INSCRIPTION_STRING ||
                                            (uint)stat.Key == (uint)STypeString.SCRIBE_NAME_STRING ||
                                            (uint)stat.Key == (uint)STypeString.SCRIBE_ACCOUNT_STRING ||
                                            (uint)stat.Key == (uint)STypeString.MONARCHS_NAME_STRING ||
                                            (uint)stat.Key == (uint)STypeString.FELLOWSHIP_STRING ||
                                            (uint)stat.Key == (uint)STypeString.CRAFTSMAN_NAME_STRING ||
                                            (uint)stat.Key == (uint)STypeString.IMBUER_NAME_STRING ||
                                            (uint)stat.Key == (uint)STypeString.HOUSE_OWNER_NAME_STRING ||
                                            (uint)stat.Key == (uint)STypeString.TINKER_NAME_STRING ||
                                            (uint)stat.Key == (uint)STypeString.ALLEGIANCE_NAME_STRING // ||
                                                                                                       //stat.Key != STypeString. ||
                                            )
                                            continue;

                                        strsLine += $"     , ({parsed.i_objid}, {(uint)stat.Key}, '{stat.Value.m_buffer?.Replace("'", "''")}') /* {Enum.GetName(typeof(STypeString), stat.Key)} */" + Environment.NewLine;
                                    }
                                }

                                if (parsed.i_prof._didStatsTable.hashTable.Count > 0)
                                {
                                    foreach (var stat in parsed.i_prof._didStatsTable.hashTable)
                                    {
                                        didsLine += $"     , ({parsed.i_objid}, {(uint)stat.Key}, {(uint)stat.Value}) /* {Enum.GetName(typeof(STypeDID), stat.Key)} */" + Environment.NewLine;
                                    }
                                }

                                if (parsed.i_prof._intStatsTable.hashTable.Count > 0)
                                {
                                    foreach (var stat in parsed.i_prof._intStatsTable.hashTable)
                                    {
                                        if (
                                            (uint)stat.Key == (uint)STypeInt.STRUCTURE_INT ||
                                            (uint)stat.Key == (uint)STypeInt.STACK_SIZE_INT ||
                                            (uint)stat.Key == (uint)STypeInt.ITEM_CUR_MANA_INT // ||
                                            )
                                            continue;

                                            intsLine += $"     , ({parsed.i_objid}, {(uint)stat.Key}, {(uint)stat.Value}) /* {Enum.GetName(typeof(STypeInt), stat.Key)} */" + Environment.NewLine;
                                    }
                                }

                                if (parsed.i_prof._int64StatsTable.hashTable.Count > 0)
                                {
                                    foreach (var stat in parsed.i_prof._int64StatsTable.hashTable)
                                    {
                                        bigintsLine += $"     , ({parsed.i_objid}, {(uint)stat.Key}, {(uint)stat.Value}) /* {Enum.GetName(typeof(STypeInt64), stat.Key)} */" + Environment.NewLine;
                                    }
                                }

                                if (parsed.i_prof._floatStatsTable.hashTable.Count > 0)
                                {
                                    foreach (var stat in parsed.i_prof._floatStatsTable.hashTable)
                                    {
                                        if (float.IsInfinity((float)stat.Value))
                                            floatsLine += $"     , ({parsed.i_objid}, {(uint)stat.Key}, {(float)Convert.ToDouble(stat.Value.ToString().Substring(0, 5))}) /* {Enum.GetName(typeof(STypeFloat), stat.Key)} */" + Environment.NewLine;
                                        else
                                            floatsLine += $"     , ({parsed.i_objid}, {(uint)stat.Key}, {(float)stat.Value}) /* {Enum.GetName(typeof(STypeFloat), stat.Key)} */" + Environment.NewLine;
                                    }
                                }

                                if (parsed.i_prof._boolStatsTable.hashTable.Count > 0)
                                {
                                    foreach (var stat in parsed.i_prof._boolStatsTable.hashTable)
                                    {
                                        boolsLine += $"     , ({parsed.i_objid}, {(uint)stat.Key}, {(uint)stat.Value}) /* {Enum.GetName(typeof(STypeBool), stat.Key)} */" + Environment.NewLine;
                                    }
                                }

                                if (parsed.i_prof._spellsTable.list.Count > 0)
                                {
                                    foreach (var stat in parsed.i_prof._spellsTable.list)
                                    {
                                        if (Enum.IsDefined(typeof(SpellID), stat))
                                            spellsLine += $"     , ({parsed.i_objid}, {(uint)stat}) /* {Enum.GetName(typeof(SpellID), stat)} */" + Environment.NewLine;
                                    }
                                }

                                if ((parsed.i_prof.header & (uint)CM_Examine.AppraisalProfile.AppraisalProfilePackHeader.Packed_WeaponProfile) != 0)
                                {
                                    intsLine += $"     , ({parsed.i_objid}, {(uint)STypeInt.DAMAGE_TYPE_INT}, {(uint)parsed.i_prof._weaponProfileTable._damage_type}) /* {Enum.GetName(typeof(STypeInt), STypeInt.DAMAGE_TYPE_INT)} */" + Environment.NewLine;
                                    intsLine += $"     , ({parsed.i_objid}, {(uint)STypeInt.WEAPON_TIME_INT}, {(uint)parsed.i_prof._weaponProfileTable._weapon_time}) /* {Enum.GetName(typeof(STypeInt), STypeInt.WEAPON_TIME_INT)} */" + Environment.NewLine;
                                    intsLine += $"     , ({parsed.i_objid}, {(uint)STypeInt.WEAPON_SKILL_INT}, {(uint)parsed.i_prof._weaponProfileTable._weapon_skill}) /* {Enum.GetName(typeof(STypeInt), STypeInt.WEAPON_SKILL_INT)} */" + Environment.NewLine;
                                    intsLine += $"     , ({parsed.i_objid}, {(uint)STypeInt.DAMAGE_INT}, {(uint)parsed.i_prof._weaponProfileTable._weapon_damage}) /* {Enum.GetName(typeof(STypeInt), STypeInt.DAMAGE_INT)} */" + Environment.NewLine;
                                    floatsLine += $"     , ({parsed.i_objid}, {(uint)STypeFloat.DAMAGE_VARIANCE_FLOAT}, {(float)parsed.i_prof._weaponProfileTable._damage_variance}) /* {Enum.GetName(typeof(STypeFloat), STypeFloat.DAMAGE_VARIANCE_FLOAT)} */" + Environment.NewLine;
                                    floatsLine += $"     , ({parsed.i_objid}, {(uint)STypeFloat.DAMAGE_MOD_FLOAT}, {(float)parsed.i_prof._weaponProfileTable._damage_mod}) /* {Enum.GetName(typeof(STypeFloat), STypeFloat.DAMAGE_MOD_FLOAT)} */" + Environment.NewLine;
                                    floatsLine += $"     , ({parsed.i_objid}, {(uint)STypeFloat.WEAPON_LENGTH_FLOAT}, {(float)parsed.i_prof._weaponProfileTable._weapon_length}) /* {Enum.GetName(typeof(STypeFloat), STypeFloat.WEAPON_LENGTH_FLOAT)} */" + Environment.NewLine;
                                    floatsLine += $"     , ({parsed.i_objid}, {(uint)STypeFloat.MAXIMUM_VELOCITY_FLOAT}, {(float)parsed.i_prof._weaponProfileTable._max_velocity}) /* {Enum.GetName(typeof(STypeFloat), STypeFloat.MAXIMUM_VELOCITY_FLOAT)} */" + Environment.NewLine;
                                    floatsLine += $"     , ({parsed.i_objid}, {(uint)STypeFloat.WEAPON_OFFENSE_FLOAT}, {(float)parsed.i_prof._weaponProfileTable._weapon_offense}) /* {Enum.GetName(typeof(STypeFloat), STypeFloat.WEAPON_OFFENSE_FLOAT)} */" + Environment.NewLine;
                                    //intsLine += $"     , ({parsed.i_objid}, {(uint)STypeInt.???}, {(uint)parsed.i_prof._weaponProfileTable._max_velocity_estimated})" + Environment.NewLine;
                                }

                                if ((parsed.i_prof.header & (uint)CM_Examine.AppraisalProfile.AppraisalProfilePackHeader.Packed_HookProfile) != 0)
                                {
                                    intsLine += $"     , ({parsed.i_objid}, {(uint)STypeInt.LOCATIONS_INT}, {(uint)parsed.i_prof._hookProfileTable._validLocations}) /* {Enum.GetName(typeof(STypeInt), STypeInt.LOCATIONS_INT)} */" + Environment.NewLine;
                                    intsLine += $"     , ({parsed.i_objid}, {(uint)STypeInt.AMMO_TYPE_INT}, {(uint)parsed.i_prof._hookProfileTable._ammoType}) /* {Enum.GetName(typeof(STypeInt), STypeInt.AMMO_TYPE_INT)} */" + Environment.NewLine;
                                    boolsLine += $"     , ({parsed.i_objid}, {(uint)STypeBool.INSCRIBABLE_BOOL}, {parsed.i_prof._hookProfileTable.isInscribable}) /* {Enum.GetName(typeof(STypeBool), STypeBool.INSCRIBABLE_BOOL)} */" + Environment.NewLine;
                                    //boolsLine += $"     , ({parsed.i_objid}, {(uint)STypeBool.???}, {parsed.i_prof._hookProfileTable.isHealer})" + Environment.NewLine;
                                    //boolsLine += $"     , ({parsed.i_objid}, {(uint)STypeBool.???}, {parsed.i_prof._hookProfileTable.isLockpick})" + Environment.NewLine;
                                }

                                if ((parsed.i_prof.header & (uint)CM_Examine.AppraisalProfile.AppraisalProfilePackHeader.Packed_ArmorProfile) != 0)
                                {
                                    floatsLine += $"     , ({parsed.i_objid}, {(uint)STypeFloat.ARMOR_MOD_VS_SLASH_FLOAT}, {(float)parsed.i_prof._armorProfileTable._mod_vs_slash}) /* {Enum.GetName(typeof(STypeFloat), STypeFloat.ARMOR_MOD_VS_SLASH_FLOAT)} */" + Environment.NewLine;
                                    floatsLine += $"     , ({parsed.i_objid}, {(uint)STypeFloat.ARMOR_MOD_VS_PIERCE_FLOAT}, {(float)parsed.i_prof._armorProfileTable._mod_vs_pierce}) /* {Enum.GetName(typeof(STypeFloat), STypeFloat.ARMOR_MOD_VS_PIERCE_FLOAT)} */" + Environment.NewLine;
                                    floatsLine += $"     , ({parsed.i_objid}, {(uint)STypeFloat.ARMOR_MOD_VS_BLUDGEON_FLOAT}, {(float)parsed.i_prof._armorProfileTable._mod_vs_bludgeon}) /* {Enum.GetName(typeof(STypeFloat), STypeFloat.ARMOR_MOD_VS_BLUDGEON_FLOAT)} */" + Environment.NewLine;
                                    floatsLine += $"     , ({parsed.i_objid}, {(uint)STypeFloat.ARMOR_MOD_VS_COLD_FLOAT}, {(float)parsed.i_prof._armorProfileTable._mod_vs_cold}) /* {Enum.GetName(typeof(STypeFloat), STypeFloat.ARMOR_MOD_VS_COLD_FLOAT)} */" + Environment.NewLine;
                                    floatsLine += $"     , ({parsed.i_objid}, {(uint)STypeFloat.ARMOR_MOD_VS_FIRE_FLOAT}, {(float)parsed.i_prof._armorProfileTable._mod_vs_fire}) /* {Enum.GetName(typeof(STypeFloat), STypeFloat.ARMOR_MOD_VS_FIRE_FLOAT)} */" + Environment.NewLine;
                                    floatsLine += $"     , ({parsed.i_objid}, {(uint)STypeFloat.ARMOR_MOD_VS_ACID_FLOAT}, {(float)parsed.i_prof._armorProfileTable._mod_vs_acid}) /* {Enum.GetName(typeof(STypeFloat), STypeFloat.ARMOR_MOD_VS_ACID_FLOAT)} */" + Environment.NewLine;
                                    floatsLine += $"     , ({parsed.i_objid}, {(uint)STypeFloat.ARMOR_MOD_VS_ELECTRIC_FLOAT}, {(float)parsed.i_prof._armorProfileTable._mod_vs_electric}) /* {Enum.GetName(typeof(STypeFloat), STypeFloat.ARMOR_MOD_VS_ELECTRIC_FLOAT)} */" + Environment.NewLine;
                                    floatsLine += $"     , ({parsed.i_objid}, {(uint)STypeFloat.ARMOR_MOD_VS_NETHER_FLOAT}, {(float)parsed.i_prof._armorProfileTable._mod_vs_nether}) /* {Enum.GetName(typeof(STypeFloat), STypeFloat.ARMOR_MOD_VS_NETHER_FLOAT)} */" + Environment.NewLine;
                                }

                                if ((parsed.i_prof.header & (uint)CM_Examine.AppraisalProfile.AppraisalProfilePackHeader.Packed_CreatureProfile) != 0)
                                {
                                    if (parsed.i_prof.success_flag == 0)
                                    {
                                        ////attribute2ndsLine += $"     , ({parsed.i_objid}, {(uint)STypeAttribute2nd.HEALTH_ATTRIBUTE_2ND}, {(uint)parsed.i_prof._creatureProfileTable._health})" + Environment.NewLine;
                                        attribute2ndsLine += $"     , ({parsed.i_objid}, {(uint)STypeAttribute2nd.MAX_HEALTH_ATTRIBUTE_2ND}, {(uint)parsed.i_prof._creatureProfileTable._max_health}) /* {Enum.GetName(typeof(STypeAttribute2nd), STypeAttribute2nd.MAX_HEALTH_ATTRIBUTE_2ND)} */" + Environment.NewLine;
                                    }
                                    else
                                    {
                                        attributesLine += $"     , ({parsed.i_objid}, {(uint)STypeAttribute.STRENGTH_ATTRIBUTE}, {(uint)parsed.i_prof._creatureProfileTable._strength}) /* {Enum.GetName(typeof(STypeAttribute), STypeAttribute.STRENGTH_ATTRIBUTE)} */" + Environment.NewLine;
                                        attributesLine += $"     , ({parsed.i_objid}, {(uint)STypeAttribute.ENDURANCE_ATTRIBUTE}, {(uint)parsed.i_prof._creatureProfileTable._endurance}) /* {Enum.GetName(typeof(STypeAttribute), STypeAttribute.ENDURANCE_ATTRIBUTE)} */" + Environment.NewLine;
                                        attributesLine += $"     , ({parsed.i_objid}, {(uint)STypeAttribute.COORDINATION_ATTRIBUTE}, {(uint)parsed.i_prof._creatureProfileTable._coordination}) /* {Enum.GetName(typeof(STypeAttribute), STypeAttribute.COORDINATION_ATTRIBUTE)} */" + Environment.NewLine;
                                        attributesLine += $"     , ({parsed.i_objid}, {(uint)STypeAttribute.QUICKNESS_ATTRIBUTE}, {(uint)parsed.i_prof._creatureProfileTable._quickness}) /* {Enum.GetName(typeof(STypeAttribute), STypeAttribute.QUICKNESS_ATTRIBUTE)} */" + Environment.NewLine;
                                        attributesLine += $"     , ({parsed.i_objid}, {(uint)STypeAttribute.FOCUS_ATTRIBUTE}, {(uint)parsed.i_prof._creatureProfileTable._focus}) /* {Enum.GetName(typeof(STypeAttribute), STypeAttribute.FOCUS_ATTRIBUTE)} */" + Environment.NewLine;
                                        attributesLine += $"     , ({parsed.i_objid}, {(uint)STypeAttribute.SELF_ATTRIBUTE}, {(uint)parsed.i_prof._creatureProfileTable._self}) /* {Enum.GetName(typeof(STypeAttribute), STypeAttribute.SELF_ATTRIBUTE)} */" + Environment.NewLine;


                                        ////attribute2ndsLine += $"     , ({parsed.i_objid}, {(uint)STypeAttribute2nd.HEALTH_ATTRIBUTE_2ND}, {(uint)parsed.i_prof._creatureProfileTable._health})" + Environment.NewLine;
                                        ////attribute2ndsLine += $"     , ({parsed.i_objid}, {(uint)STypeAttribute2nd.STAMINA_ATTRIBUTE_2ND}, {(uint)parsed.i_prof._creatureProfileTable._stamina})" + Environment.NewLine;
                                        ////attribute2ndsLine += $"     , ({parsed.i_objid}, {(uint)STypeAttribute2nd.MANA_ATTRIBUTE_2ND}, {(uint)parsed.i_prof._creatureProfileTable._mana})" + Environment.NewLine;

                                        attribute2ndsLine += $"     , ({parsed.i_objid}, {(uint)STypeAttribute2nd.MAX_HEALTH_ATTRIBUTE_2ND}, {(uint)parsed.i_prof._creatureProfileTable._max_health}) /* {Enum.GetName(typeof(STypeAttribute2nd), STypeAttribute2nd.MAX_HEALTH_ATTRIBUTE_2ND)} */" + Environment.NewLine;
                                        attribute2ndsLine += $"     , ({parsed.i_objid}, {(uint)STypeAttribute2nd.MAX_STAMINA_ATTRIBUTE_2ND}, {(uint)parsed.i_prof._creatureProfileTable._max_stamina}) /* {Enum.GetName(typeof(STypeAttribute2nd), STypeAttribute2nd.MAX_STAMINA_ATTRIBUTE_2ND)} */" + Environment.NewLine;
                                        attribute2ndsLine += $"     , ({parsed.i_objid}, {(uint)STypeAttribute2nd.MAX_MANA_ATTRIBUTE_2ND}, {(uint)parsed.i_prof._creatureProfileTable._max_mana}) /* {Enum.GetName(typeof(STypeAttribute2nd), STypeAttribute2nd.MAX_MANA_ATTRIBUTE_2ND)} */" + Environment.NewLine;
                                    }
                                }

                                if (strsLine != "")
                                {
                                    strsLine = $"{sqlCommand} INTO `ace_object_properties_string` (`aceObjectId`, `strPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + strsLine.TrimStart("     ,".ToCharArray());
                                    strsLine = strsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(strsLine);
                                }
                                if (didsLine != "")
                                {
                                    didsLine = $"{sqlCommand} INTO `ace_object_properties_did` (`aceObjectId`, `didPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + didsLine.TrimStart("     ,".ToCharArray());
                                    didsLine = didsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(didsLine);
                                }
                                if (iidsLine != "")
                                {
                                    iidsLine = $"{sqlCommand} INTO `ace_object_properties_iid` (`aceObjectId`, `iidPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + iidsLine.TrimStart("     ,".ToCharArray());
                                    iidsLine = iidsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(iidsLine);
                                }
                                if (intsLine != "")
                                {
                                    intsLine = $"{sqlCommand} INTO `ace_object_properties_int` (`aceObjectId`, `intPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + intsLine.TrimStart("     ,".ToCharArray());
                                    intsLine = intsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(intsLine);
                                }
                                if (bigintsLine != "")
                                {
                                    bigintsLine = $"{sqlCommand} INTO `ace_object_properties_bigint` (`aceObjectId`, `bigIntPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + bigintsLine.TrimStart("     ,".ToCharArray());
                                    bigintsLine = bigintsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(bigintsLine);
                                }
                                if (floatsLine != "")
                                {
                                    floatsLine = $"{sqlCommand} INTO `ace_object_properties_double` (`aceObjectId`, `dblPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + floatsLine.TrimStart("     ,".ToCharArray());
                                    floatsLine = floatsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(floatsLine);
                                }
                                if (boolsLine != "")
                                {
                                    boolsLine = $"{sqlCommand} INTO `ace_object_properties_bool` (`aceObjectId`, `boolPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + boolsLine.TrimStart("     ,".ToCharArray());
                                    boolsLine = boolsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(boolsLine);
                                }
                                if (spellsLine != "")
                                {
                                    spellsLine = $"{sqlCommand} INTO `ace_object_properties_spell` (`aceObjectId`, `spellId`)" + Environment.NewLine
                                        + "VALUES " + spellsLine.TrimStart("     ,".ToCharArray());
                                    spellsLine = spellsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(spellsLine);
                                }
                                if (attributesLine != "")
                                {
                                    attributesLine = $"{sqlCommand} INTO `ace_object_properties_attribute` (`aceObjectId`, `attributeId`, `attributeBase`)" + Environment.NewLine
                                        + "VALUES " + attributesLine.TrimStart("     ,".ToCharArray());
                                    attributesLine = attributesLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(attributesLine);
                                }
                                if (attribute2ndsLine != "")
                                {
                                    attribute2ndsLine = $"{sqlCommand} INTO `ace_object_properties_attribute2nd` (`aceObjectId`, `attribute2ndId`, `attribute2ndValue`)" + Environment.NewLine
                                        + "VALUES " + attribute2ndsLine.TrimStart("     ,".ToCharArray());
                                    attribute2ndsLine = attribute2ndsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(attribute2ndsLine);
                                }

                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("Unable to export object " + parsed.i_objid + ". Exception:" + Environment.NewLine + ex.ToString());
                    }
                }
            }
        }

        private void WriteWeenieAppraisalObjectData(Dictionary<string, List<CM_Examine.SetAppraiseInfo>> appraisalObjects, string outputFolder)
        {
            string staticFolder = Path.Combine(outputFolder, "2-weenieapprasialobjects");

            //string sqlCommand = "INSERT";
            string sqlCommand = "REPLACE";

            if (!Directory.Exists(staticFolder))
                Directory.CreateDirectory(staticFolder);

            Dictionary<string, int> fileCount = new Dictionary<string, int>();

            foreach (string key in appraisalObjects.Keys)
            {
                foreach (var parsed in appraisalObjects[key])
                {

                    if (parsed.i_objid > 65535)
                        continue;

                    try
                    {
                        if (!fileCount.ContainsKey(key))
                            fileCount.Add(key, 0);

                        string fullFile = Path.Combine(staticFolder, $"{key}_{fileCount[key]}.sql");

                        if (File.Exists(fullFile))
                        {
                            FileInfo fi = new FileInfo(fullFile);

                            // go to the next file if it's bigger than a MB
                            if (fi.Length > ((1048576) * 40))
                            {
                                fileCount[key]++;
                                fullFile = Path.Combine(staticFolder, $"{key}_{fileCount[key]}.sql");

                                if (File.Exists(fullFile))
                                    File.Delete(fullFile);
                            }
                        }

                        using (FileStream fs = new FileStream(fullFile, FileMode.Append))
                        {
                            using (StreamWriter writer = new StreamWriter(fs))
                            {

                                if (parsed.i_objid > 65535)
                                    continue;

                                string intsLine = "", bigintsLine = "", floatsLine = "", boolsLine = "", strsLine = "", didsLine = "", iidsLine = "";
                                string skillsLine = "", attributesLine = "", attribute2ndsLine = "", bodyDamageValuesLine = "", bodyDamageVariancesLine = "", bodyArmorValuesLine = "", numsLine = "";
                                string spellsLine = ""; //, bigintsLine = "", floatsLine = "", boolsLine = "", strsLine = "", didsLine = "", iidsLine = "";

                                if (parsed.i_prof._strStatsTable.hashTable.Count > 0)
                                {
                                    foreach (var stat in parsed.i_prof._strStatsTable.hashTable)
                                    {
                                        strsLine += $"     , ({parsed.i_objid}, {(uint)stat.Key}, '{stat.Value.m_buffer?.Replace("'", "''")}')" + Environment.NewLine;
                                    }
                                }

                                if (parsed.i_prof._didStatsTable.hashTable.Count > 0)
                                {
                                    foreach (var stat in parsed.i_prof._didStatsTable.hashTable)
                                    {
                                        didsLine += $"     , ({parsed.i_objid}, {(uint)stat.Key}, {(uint)stat.Value})" + Environment.NewLine;
                                    }
                                }

                                if (parsed.i_prof._intStatsTable.hashTable.Count > 0)
                                {
                                    foreach (var stat in parsed.i_prof._intStatsTable.hashTable)
                                    {
                                        intsLine += $"     , ({parsed.i_objid}, {(uint)stat.Key}, {(uint)stat.Value})" + Environment.NewLine;
                                    }
                                }

                                if (parsed.i_prof._int64StatsTable.hashTable.Count > 0)
                                {
                                    foreach (var stat in parsed.i_prof._int64StatsTable.hashTable)
                                    {
                                        bigintsLine += $"     , ({parsed.i_objid}, {(uint)stat.Key}, {(uint)stat.Value})" + Environment.NewLine;
                                    }
                                }

                                if (parsed.i_prof._floatStatsTable.hashTable.Count > 0)
                                {
                                    foreach (var stat in parsed.i_prof._floatStatsTable.hashTable)
                                    {
                                        if (float.IsInfinity((float)stat.Value))
                                            floatsLine += $"     , ({parsed.i_objid}, {(uint)stat.Key}, {(float)Convert.ToDouble(stat.Value.ToString().Substring(0, 5))})" + Environment.NewLine;
                                        else
                                            floatsLine += $"     , ({parsed.i_objid}, {(uint)stat.Key}, {(float)stat.Value})" + Environment.NewLine;
                                    }
                                }

                                if (parsed.i_prof._boolStatsTable.hashTable.Count > 0)
                                {
                                    foreach (var stat in parsed.i_prof._boolStatsTable.hashTable)
                                    {
                                        boolsLine += $"     , ({parsed.i_objid}, {(uint)stat.Key}, {(uint)stat.Value})" + Environment.NewLine;
                                    }
                                }

                                if (parsed.i_prof._spellsTable.list.Count > 0)
                                {
                                    foreach (var stat in parsed.i_prof._spellsTable.list)
                                    {
                                        if (Enum.IsDefined(typeof(SpellID), stat))
                                            spellsLine += $"     , ({parsed.i_objid}, {(uint)stat})" + Environment.NewLine;
                                    }
                                }

                                if ((parsed.i_prof.header & (uint)CM_Examine.AppraisalProfile.AppraisalProfilePackHeader.Packed_WeaponProfile) != 0)
                                {
                                    intsLine += $"     , ({parsed.i_objid}, {(uint)STypeInt.DAMAGE_TYPE_INT}, {(uint)parsed.i_prof._weaponProfileTable._damage_type})" + Environment.NewLine;
                                    intsLine += $"     , ({parsed.i_objid}, {(uint)STypeInt.WEAPON_TIME_INT}, {(uint)parsed.i_prof._weaponProfileTable._weapon_time})" + Environment.NewLine;
                                    intsLine += $"     , ({parsed.i_objid}, {(uint)STypeInt.WEAPON_SKILL_INT}, {(uint)parsed.i_prof._weaponProfileTable._weapon_skill})" + Environment.NewLine;
                                    intsLine += $"     , ({parsed.i_objid}, {(uint)STypeInt.DAMAGE_INT}, {(uint)parsed.i_prof._weaponProfileTable._weapon_damage})" + Environment.NewLine;
                                    floatsLine += $"     , ({parsed.i_objid}, {(uint)STypeFloat.DAMAGE_VARIANCE_FLOAT}, {(float)parsed.i_prof._weaponProfileTable._damage_variance})" + Environment.NewLine;
                                    floatsLine += $"     , ({parsed.i_objid}, {(uint)STypeFloat.DAMAGE_MOD_FLOAT}, {(float)parsed.i_prof._weaponProfileTable._damage_mod})" + Environment.NewLine;
                                    floatsLine += $"     , ({parsed.i_objid}, {(uint)STypeFloat.WEAPON_LENGTH_FLOAT}, {(float)parsed.i_prof._weaponProfileTable._weapon_length})" + Environment.NewLine;
                                    floatsLine += $"     , ({parsed.i_objid}, {(uint)STypeFloat.MAXIMUM_VELOCITY_FLOAT}, {(float)parsed.i_prof._weaponProfileTable._max_velocity})" + Environment.NewLine;
                                    floatsLine += $"     , ({parsed.i_objid}, {(uint)STypeFloat.WEAPON_OFFENSE_FLOAT}, {(float)parsed.i_prof._weaponProfileTable._weapon_offense})" + Environment.NewLine;
                                    //intsLine += $"     , ({parsed.i_objid}, {(uint)STypeInt.???}, {(uint)parsed.i_prof._weaponProfileTable._max_velocity_estimated})" + Environment.NewLine;
                                }

                                if ((parsed.i_prof.header & (uint)CM_Examine.AppraisalProfile.AppraisalProfilePackHeader.Packed_HookProfile) != 0)
                                {
                                    intsLine += $"     , ({parsed.i_objid}, {(uint)STypeInt.LOCATIONS_INT}, {(uint)parsed.i_prof._hookProfileTable._validLocations})" + Environment.NewLine;
                                    intsLine += $"     , ({parsed.i_objid}, {(uint)STypeInt.AMMO_TYPE_INT}, {(uint)parsed.i_prof._hookProfileTable._ammoType})" + Environment.NewLine;
                                    boolsLine += $"     , ({parsed.i_objid}, {(uint)STypeBool.INSCRIBABLE_BOOL}, {parsed.i_prof._hookProfileTable.isInscribable})" + Environment.NewLine;
                                    //boolsLine += $"     , ({parsed.i_objid}, {(uint)STypeBool.???}, {parsed.i_prof._hookProfileTable.isHealer})" + Environment.NewLine;
                                    //boolsLine += $"     , ({parsed.i_objid}, {(uint)STypeBool.???}, {parsed.i_prof._hookProfileTable.isLockpick})" + Environment.NewLine;
                                }

                                if ((parsed.i_prof.header & (uint)CM_Examine.AppraisalProfile.AppraisalProfilePackHeader.Packed_ArmorProfile) != 0)
                                {
                                    floatsLine += $"     , ({parsed.i_objid}, {(uint)STypeFloat.ARMOR_MOD_VS_SLASH_FLOAT}, {(float)parsed.i_prof._armorProfileTable._mod_vs_slash})" + Environment.NewLine;
                                    floatsLine += $"     , ({parsed.i_objid}, {(uint)STypeFloat.ARMOR_MOD_VS_PIERCE_FLOAT}, {(float)parsed.i_prof._armorProfileTable._mod_vs_pierce})" + Environment.NewLine;
                                    floatsLine += $"     , ({parsed.i_objid}, {(uint)STypeFloat.ARMOR_MOD_VS_BLUDGEON_FLOAT}, {(float)parsed.i_prof._armorProfileTable._mod_vs_bludgeon})" + Environment.NewLine;
                                    floatsLine += $"     , ({parsed.i_objid}, {(uint)STypeFloat.ARMOR_MOD_VS_COLD_FLOAT}, {(float)parsed.i_prof._armorProfileTable._mod_vs_cold})" + Environment.NewLine;
                                    floatsLine += $"     , ({parsed.i_objid}, {(uint)STypeFloat.ARMOR_MOD_VS_FIRE_FLOAT}, {(float)parsed.i_prof._armorProfileTable._mod_vs_fire})" + Environment.NewLine;
                                    floatsLine += $"     , ({parsed.i_objid}, {(uint)STypeFloat.ARMOR_MOD_VS_ACID_FLOAT}, {(float)parsed.i_prof._armorProfileTable._mod_vs_acid})" + Environment.NewLine;
                                    floatsLine += $"     , ({parsed.i_objid}, {(uint)STypeFloat.ARMOR_MOD_VS_ELECTRIC_FLOAT}, {(float)parsed.i_prof._armorProfileTable._mod_vs_electric})" + Environment.NewLine;
                                    floatsLine += $"     , ({parsed.i_objid}, {(uint)STypeFloat.ARMOR_MOD_VS_NETHER_FLOAT}, {(float)parsed.i_prof._armorProfileTable._mod_vs_nether})" + Environment.NewLine;
                                }

                                if ((parsed.i_prof.header & (uint)CM_Examine.AppraisalProfile.AppraisalProfilePackHeader.Packed_CreatureProfile) != 0)
                                {
                                    if (parsed.i_prof.success_flag == 0)
                                    {
                                        ////attribute2ndsLine += $"     , ({parsed.i_objid}, {(uint)STypeAttribute2nd.HEALTH_ATTRIBUTE_2ND}, {(uint)parsed.i_prof._creatureProfileTable._health})" + Environment.NewLine;
                                        attribute2ndsLine += $"     , ({parsed.i_objid}, {(uint)STypeAttribute2nd.MAX_HEALTH_ATTRIBUTE_2ND}, {(uint)parsed.i_prof._creatureProfileTable._max_health})" + Environment.NewLine;
                                    }
                                    else
                                    {
                                        attributesLine += $"     , ({parsed.i_objid}, {(uint)STypeAttribute.STRENGTH_ATTRIBUTE}, {(uint)parsed.i_prof._creatureProfileTable._strength})" + Environment.NewLine;
                                        attributesLine += $"     , ({parsed.i_objid}, {(uint)STypeAttribute.ENDURANCE_ATTRIBUTE}, {(uint)parsed.i_prof._creatureProfileTable._endurance})" + Environment.NewLine;
                                        attributesLine += $"     , ({parsed.i_objid}, {(uint)STypeAttribute.COORDINATION_ATTRIBUTE}, {(uint)parsed.i_prof._creatureProfileTable._coordination})" + Environment.NewLine;
                                        attributesLine += $"     , ({parsed.i_objid}, {(uint)STypeAttribute.QUICKNESS_ATTRIBUTE}, {(uint)parsed.i_prof._creatureProfileTable._quickness})" + Environment.NewLine;
                                        attributesLine += $"     , ({parsed.i_objid}, {(uint)STypeAttribute.FOCUS_ATTRIBUTE}, {(uint)parsed.i_prof._creatureProfileTable._focus})" + Environment.NewLine;
                                        attributesLine += $"     , ({parsed.i_objid}, {(uint)STypeAttribute.SELF_ATTRIBUTE}, {(uint)parsed.i_prof._creatureProfileTable._self})" + Environment.NewLine;


                                        ////attribute2ndsLine += $"     , ({parsed.i_objid}, {(uint)STypeAttribute2nd.HEALTH_ATTRIBUTE_2ND}, {(uint)parsed.i_prof._creatureProfileTable._health})" + Environment.NewLine;
                                        ////attribute2ndsLine += $"     , ({parsed.i_objid}, {(uint)STypeAttribute2nd.STAMINA_ATTRIBUTE_2ND}, {(uint)parsed.i_prof._creatureProfileTable._stamina})" + Environment.NewLine;
                                        ////attribute2ndsLine += $"     , ({parsed.i_objid}, {(uint)STypeAttribute2nd.MANA_ATTRIBUTE_2ND}, {(uint)parsed.i_prof._creatureProfileTable._mana})" + Environment.NewLine;

                                        attribute2ndsLine += $"     , ({parsed.i_objid}, {(uint)STypeAttribute2nd.MAX_HEALTH_ATTRIBUTE_2ND}, {(uint)parsed.i_prof._creatureProfileTable._max_health})" + Environment.NewLine;
                                        attribute2ndsLine += $"     , ({parsed.i_objid}, {(uint)STypeAttribute2nd.MAX_STAMINA_ATTRIBUTE_2ND}, {(uint)parsed.i_prof._creatureProfileTable._max_stamina})" + Environment.NewLine;
                                        attribute2ndsLine += $"     , ({parsed.i_objid}, {(uint)STypeAttribute2nd.MAX_MANA_ATTRIBUTE_2ND}, {(uint)parsed.i_prof._creatureProfileTable._max_mana})" + Environment.NewLine;
                                    }
                                }

                                if (strsLine != "")
                                {
                                    strsLine = $"{sqlCommand} INTO `ace_object_properties_string` (`aceObjectId`, `strPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + strsLine.TrimStart("     ,".ToCharArray());
                                    strsLine = strsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(strsLine);
                                }
                                if (didsLine != "")
                                {
                                    didsLine = $"{sqlCommand} INTO `ace_object_properties_did` (`aceObjectId`, `didPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + didsLine.TrimStart("     ,".ToCharArray());
                                    didsLine = didsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(didsLine);
                                }
                                if (iidsLine != "")
                                {
                                    iidsLine = $"{sqlCommand} INTO `ace_object_properties_iid` (`aceObjectId`, `iidPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + iidsLine.TrimStart("     ,".ToCharArray());
                                    iidsLine = iidsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(iidsLine);
                                }
                                if (intsLine != "")
                                {
                                    intsLine = $"{sqlCommand} INTO `ace_object_properties_int` (`aceObjectId`, `intPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + intsLine.TrimStart("     ,".ToCharArray());
                                    intsLine = intsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(intsLine);
                                }
                                if (bigintsLine != "")
                                {
                                    bigintsLine = $"{sqlCommand} INTO `ace_object_properties_bigint` (`aceObjectId`, `bigIntPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + bigintsLine.TrimStart("     ,".ToCharArray());
                                    bigintsLine = bigintsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(bigintsLine);
                                }
                                if (floatsLine != "")
                                {
                                    floatsLine = $"{sqlCommand} INTO `ace_object_properties_double` (`aceObjectId`, `dblPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + floatsLine.TrimStart("     ,".ToCharArray());
                                    floatsLine = floatsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(floatsLine);
                                }
                                if (boolsLine != "")
                                {
                                    boolsLine = $"{sqlCommand} INTO `ace_object_properties_bool` (`aceObjectId`, `boolPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + boolsLine.TrimStart("     ,".ToCharArray());
                                    boolsLine = boolsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(boolsLine);
                                }
                                if (spellsLine != "")
                                {
                                    spellsLine = $"{sqlCommand} INTO `ace_object_properties_spell` (`aceObjectId`, `spellId`)" + Environment.NewLine
                                        + "VALUES " + spellsLine.TrimStart("     ,".ToCharArray());
                                    spellsLine = spellsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(spellsLine);
                                }
                                if (attributesLine != "")
                                {
                                    attributesLine = $"{sqlCommand} INTO `ace_object_properties_attribute` (`aceObjectId`, `attributeId`, `attributeBase`)" + Environment.NewLine
                                        + "VALUES " + attributesLine.TrimStart("     ,".ToCharArray());
                                    attributesLine = attributesLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(attributesLine);
                                }
                                if (attribute2ndsLine != "")
                                {
                                    attribute2ndsLine = $"{sqlCommand} INTO `ace_object_properties_attribute2nd` (`aceObjectId`, `attribute2ndId`, `attribute2ndValue`)" + Environment.NewLine
                                        + "VALUES " + attribute2ndsLine.TrimStart("     ,".ToCharArray());
                                    attribute2ndsLine = attribute2ndsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(attribute2ndsLine);
                                }

                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("Unable to export object " + parsed.i_objid + ". Exception:" + Environment.NewLine + ex.ToString());
                    }
                }
            }
        }

        private void CreateStaticObjectsList(CM_Physics.CreateObject parsed, List<uint> objectIds, Dictionary<string, List<CM_Physics.CreateObject>> staticObjects,
            bool saveAsLandblockInstances, Dictionary<uint, Dictionary<string, List<CM_Physics.CreateObject>>> landblockInstances,
            List<uint> weenieIds, Dictionary<string, List<CM_Physics.CreateObject>> weenies, Dictionary<uint, List<Position>> processedWeeniePositions, bool dedupeWeenies,
            Dictionary<uint, string> appraisalObjectsCatagoryMap, Dictionary<uint, uint> appraisalObjectToWeenieId,
            Dictionary<uint, string> weenieObjectsCatagoryMap, Dictionary<uint, uint> weeniesWeenieType, 
            Dictionary<uint, uint> staticObjectsWeenieType, Dictionary<uint, List<uint>> wieldedObjectsParentMap, Dictionary<uint, List<CM_Physics.CreateObject>> wieldedObjects,
            Dictionary<uint, List<uint>> inventoryParents, Dictionary<uint, CM_Physics.CreateObject> inventoryObjects,
            Dictionary<uint, List<uint>> parentWieldsWeenies,
            Dictionary<uint, CM_Physics.CreateObject> weeniesTypeTemplate,
            bool addEverything)
        {
            try
            {
                // don't need undefined crap or players
                //if (parsed.wdesc._wcid == 1 || objectIds.Contains(parsed.object_id))
                //if (objectIds.Contains(parsed.object_id))
                if (objectIds.Contains(parsed.object_id) && weenieIds.Contains(parsed.wdesc._wcid))
                    return;

                bool addIt = false;
                bool addWeenie = false;
                //bool addEverything = true;
                string fileToPutItIn = "Other";
                // string weeniefileToPutItIn = "OtherWeenies";
                WeenieType weenieType = WeenieType.Generic_WeenieType;
                float margin = 0.02f;

                // if ((parsed.physicsdesc.pos.objcell_id >> 16) >= 56163 && (parsed.physicsdesc.pos.objcell_id >> 16) <= 56419)
                // {

                if ((parsed.wdesc._bitfield & (uint)CM_Physics.PublicWeenieDesc.BitfieldIndex.BF_LIFESTONE) != 0)
                {
                    fileToPutItIn = "Lifestones";
                    weenieType = WeenieType.LifeStone_WeenieType;
                    addIt = true;
                }
                else if ((parsed.wdesc._bitfield & (uint)CM_Physics.PublicWeenieDesc.BitfieldIndex.BF_BINDSTONE) != 0)
                {
                    fileToPutItIn = "Bindstones";
                    weenieType = WeenieType.AllegianceBindstone_WeenieType;
                    addIt = true;
                }
                else if (parsed.wdesc._wcid == 1)
                {
                    fileToPutItIn = "Players";
                    weenieType = WeenieType.Creature_WeenieType;
                    addWeenie = true;
                }
                else if ((parsed.wdesc._bitfield & (uint)CM_Physics.PublicWeenieDesc.BitfieldIndex.BF_PKSWITCH) != 0)
                {
                    fileToPutItIn = "PKSwitches";
                    weenieType = WeenieType.PKModifier_WeenieType;
                    addIt = true;
                }
                else if ((parsed.wdesc._bitfield & (uint)CM_Physics.PublicWeenieDesc.BitfieldIndex.BF_NPKSWITCH) != 0)
                {
                    fileToPutItIn = "NPKSwitches";
                    weenieType = WeenieType.PKModifier_WeenieType;
                    addIt = true;
                }
                else if ((parsed.wdesc._bitfield & (uint)CM_Physics.PublicWeenieDesc.BitfieldIndex.BF_LOCKPICK) != 0)
                {
                    fileToPutItIn = "Lockpicks";
                    weenieType = WeenieType.Lockpick_WeenieType;
                    addWeenie = true;
                }
                else if ((parsed.wdesc._bitfield & (uint)CM_Physics.PublicWeenieDesc.BitfieldIndex.BF_FOOD) != 0)
                {
                    fileToPutItIn = "FoodObjects";
                    weenieType = WeenieType.Food_WeenieType;
                    addWeenie = true;
                }
                else if ((parsed.wdesc._bitfield & (uint)CM_Physics.PublicWeenieDesc.BitfieldIndex.BF_HEALER) != 0)
                {
                    fileToPutItIn = "Healers";
                    weenieType = WeenieType.Healer_WeenieType;
                    addWeenie = true;
                }
                else if ((parsed.wdesc._bitfield & (uint)CM_Physics.PublicWeenieDesc.BitfieldIndex.BF_BOOK) != 0)
                {
                    if (parsed.wdesc._name.m_buffer.Contains("Statue"))
                    {
                        fileToPutItIn = "BooksStatues";
                        weenieType = WeenieType.Book_WeenieType;
                        addIt = true;
                    }
                    else if (parsed.wdesc._name.m_buffer.Contains("Scroll"))
                    {
                        fileToPutItIn = "BooksScrolls";
                        weenieType = WeenieType.Book_WeenieType;
                        addWeenie = true;
                    }
                    else if (parsed.wdesc._name.m_buffer.Contains("Pack"))
                    {
                        fileToPutItIn = "BooksPackToys";
                        weenieType = WeenieType.Book_WeenieType;
                        addWeenie = true;
                    }
                    else if (parsed.wdesc._wcid == 9002)
                    {
                        fileToPutItIn = "BooksShardVigil";
                        weenieType = WeenieType.Book_WeenieType;
                        addIt = true;
                    }
                    else if (parsed.wdesc._wcid == 12774
                        || parsed.wdesc._wcid == 16908
                        )
                    {
                        fileToPutItIn = "HouseBooks";
                        weenieType = WeenieType.Book_WeenieType;
                        addIt = true;
                    }
                    else if (parsed.object_id < 0x80000000)
                    {
                        fileToPutItIn = "BooksStatics";
                        weenieType = WeenieType.Book_WeenieType;
                        addIt = true;
                    }
                    else
                    {
                        fileToPutItIn = "Books";
                        weenieType = WeenieType.Book_WeenieType;
                        addWeenie = true;
                    }
                }
                else if ((parsed.wdesc._bitfield & (uint)CM_Physics.PublicWeenieDesc.BitfieldIndex.BF_PORTAL) != 0)
                {
                    if (
                        parsed.wdesc._wcid == 9620 || // W_PORTALHOUSE_CLASS
                        parsed.wdesc._wcid == 10751 || // W_PORTALHOUSETEST_CLASS
                        parsed.wdesc._wcid == 11730    // W_HOUSEPORTAL_CLASS
                        )
                    {
                        fileToPutItIn = "HousePortals";
                        weenieType = WeenieType.HousePortal_WeenieType;
                        parsed.wdesc._bitfield = 262164; // error correct
                        parsed.wdesc.header = 41943088; // error correct
                        addIt = true;
                    }
                    else if (parsed.wdesc._wcid == 1955)
                    {
                        fileToPutItIn = "PortalsSummoned";
                        weenieType = WeenieType.Portal_WeenieType;
                        addWeenie = true;
                    }
                    else if (parsed.wdesc._name.m_buffer.Contains("Town Network"))
                    {
                        fileToPutItIn = "PortalsTownNetwork";
                        weenieType = WeenieType.Portal_WeenieType;
                        addIt = true;
                    }
                    else if (parsed.wdesc._name.m_buffer.Contains("Floating City"))
                    {
                        fileToPutItIn = "PortalsFloatingCity";
                        weenieType = WeenieType.Portal_WeenieType;
                        addIt = true;
                    }
                    else if (parsed.wdesc._name.m_buffer.Contains("Humming Crystal"))
                    {
                        fileToPutItIn = "PortalsHummingCrystal";
                        weenieType = WeenieType.Portal_WeenieType;
                        addIt = true;
                    }
                    else if (parsed.wdesc._name.m_buffer.Contains("The Orphanage"))
                    {
                        fileToPutItIn = "PortalsTheOrphanage";
                        weenieType = WeenieType.Portal_WeenieType;
                        addIt = true;
                    }
                    else if (parsed.wdesc._name.m_buffer.Contains("Golem Sanctum"))
                    {
                        fileToPutItIn = "PortalsGolemSanctum";
                        weenieType = WeenieType.Portal_WeenieType;
                        addIt = true;
                    }
                    else if (parsed.wdesc._name.m_buffer.Contains("Destroyed"))
                    {
                        fileToPutItIn = "PortalsDestroyed";
                        weenieType = WeenieType.Portal_WeenieType;
                        addIt = true;
                    }
                    else if (parsed.wdesc._name.m_buffer.Contains("Meeting Hall"))
                    {
                        fileToPutItIn = "PortalsMeetingHall";
                        weenieType = WeenieType.Portal_WeenieType;
                        addIt = true;
                    }
                    else if (parsed.wdesc._name.m_buffer.Contains("Portal to"))
                    {
                        fileToPutItIn = "PortalsPortalto";
                        weenieType = WeenieType.Portal_WeenieType;
                        addIt = true;
                    }
                    else if (parsed.wdesc._name.m_buffer.Contains("Portal"))
                    {
                        fileToPutItIn = "PortalsPortal";
                        weenieType = WeenieType.Portal_WeenieType;
                        addIt = true;
                    }
                    else
                    {
                        fileToPutItIn = "Portals";
                        weenieType = WeenieType.Portal_WeenieType;
                        addIt = true;
                    }
                }
                else if ((parsed.wdesc._bitfield & (uint)CM_Physics.PublicWeenieDesc.BitfieldIndex.BF_DOOR) != 0)
                {
                    if (parsed.wdesc._wcid == 412
                        )
                    {
                        fileToPutItIn = "DoorsAluvianHouse";
                        weenieType = WeenieType.Door_WeenieType;
                        parsed.physicsdesc.setup_id = 33561087; // error correct
                        parsed.physicsdesc.mtable_id = 150995458; // error correct
                        addIt = true;
                    }
                    else if (parsed.wdesc._wcid == 15451)
                    {
                        fileToPutItIn = "DoorsApartments";
                        weenieType = WeenieType.Door_WeenieType;
                        addIt = true;
                    }
                    else if (parsed.wdesc._wcid == 577)
                    {
                        fileToPutItIn = "DoorsPrison10";
                        weenieType = WeenieType.Door_WeenieType;
                        addIt = true;
                    }
                    else
                    {
                        fileToPutItIn = "Doors";
                        weenieType = WeenieType.Door_WeenieType;
                        addIt = true;
                    }
                }
                else if ((parsed.wdesc._bitfield & (uint)CM_Physics.PublicWeenieDesc.BitfieldIndex.BF_VENDOR) != 0)
                {
                    if (parsed.wdesc._name.m_buffer == "Babe the Blue Auroch"
                        || parsed.wdesc._name.m_buffer == "Paul the Monouga"
                        )
                    {
                        fileToPutItIn = "VendorsSpecialNPCs";
                        weenieType = WeenieType.Vendor_WeenieType;
                        addIt = true;
                        margin = 15f;
                    }
                    else if (parsed.wdesc._wcid == 43481
                        || parsed.wdesc._wcid == 43480
                        )
                    {
                        fileToPutItIn = "VendorsOlthoiPlayers";
                        weenieType = WeenieType.Vendor_WeenieType;
                        addWeenie = true;
                    }
                    else if (parsed.wdesc._name.m_buffer.Contains("Crier")
                        && parsed.wdesc._blipColor == 8)
                    {
                        fileToPutItIn = "VendorsTownCriers";
                        weenieType = WeenieType.Vendor_WeenieType;
                        addIt = true;
                        margin = 20f;
                    }
                    else if (parsed.wdesc._name.m_buffer.Contains("Pet")
                        || parsed.wdesc._name.m_buffer.Contains("Wind-up")
                        || parsed.wdesc._wcid == 48881
                        || parsed.wdesc._wcid == 34902
                        || parsed.wdesc._wcid == 48891
                        || parsed.wdesc._wcid == 48879
                        || parsed.wdesc._wcid == 34906
                        || parsed.wdesc._wcid == 48887
                        || parsed.wdesc._wcid == 48889
                        || parsed.wdesc._wcid == 48883
                        || parsed.wdesc._wcid == 34900
                        || parsed.wdesc._wcid == 34901
                        || parsed.wdesc._wcid == 34908
                        || parsed.wdesc._wcid == 34898
                        )
                    {
                        fileToPutItIn = "VendorsPets";
                        weenieType = WeenieType.Vendor_WeenieType;
                        addWeenie = true;
                    }
                    else if (parsed.wdesc._blipColor == 8)
                    {
                        fileToPutItIn = "VendorsNPCs";
                        weenieType = WeenieType.Vendor_WeenieType;
                        addIt = true;
                        margin = 15f;
                    }
                    else
                    {
                        fileToPutItIn = "Vendors";
                        weenieType = WeenieType.Vendor_WeenieType;
                        margin = 15f;
                        addIt = true;
                    }
                }
                else if (parsed.wdesc._wcid == 4)
                {
                    fileToPutItIn = "Admins";
                    weenieType = WeenieType.Admin_WeenieType;
                    addWeenie = true;
                }
                else if (parsed.wdesc._type == ITEM_TYPE.TYPE_MISC) // HOUSE OBJECTS
                {
                    if (
                           parsed.wdesc._wcid == 9548 || // W_HOUSE_CLASS
                           parsed.wdesc._wcid >= 9693 && parsed.wdesc._wcid <= 10492 || // W_HOUSECOTTAGE1_CLASS to W_HOUSECOTTAGE800_CLASS
                           parsed.wdesc._wcid >= 10493 && parsed.wdesc._wcid <= 10662 || // W_HOUSEVILLA801_CLASS to W_HOUSEVILLA970_CLASS
                           parsed.wdesc._wcid >= 10663 && parsed.wdesc._wcid <= 10692 || // W_HOUSEMANSION971_CLASS to W_HOUSEMANSION1000_CLASS
                           parsed.wdesc._wcid >= 10746 && parsed.wdesc._wcid <= 10750 || // W_HOUSETEST1_CLASS to W_HOUSETEST5_CLASS
                           parsed.wdesc._wcid >= 10829 && parsed.wdesc._wcid <= 10839 || // W_HOUSETEST6_CLASS to W_HOUSETEST16_CLASS
                           parsed.wdesc._wcid >= 11677 && parsed.wdesc._wcid <= 11682 || // W_HOUSETEST17_CLASS to W_HOUSETEST22_CLASS
                           parsed.wdesc._wcid >= 12311 && parsed.wdesc._wcid <= 12460 || // W_HOUSECOTTAGE1001_CLASS to W_HOUSECOTTAGE1150_CLASS
                           parsed.wdesc._wcid >= 12775 && parsed.wdesc._wcid <= 13024 || // W_HOUSECOTTAGE1151_CLASS to W_HOUSECOTTAGE1400_CLASS
                           parsed.wdesc._wcid >= 13025 && parsed.wdesc._wcid <= 13064 || // W_HOUSEVILLA1401_CLASS to W_HOUSEVILLA1440_CLASS
                           parsed.wdesc._wcid >= 13065 && parsed.wdesc._wcid <= 13074 || // W_HOUSEMANSION1441_CLASS to W_HOUSEMANSION1450_CLASS
                           parsed.wdesc._wcid == 13234 || // W_HOUSECOTTAGETEST10000_CLASS
                           parsed.wdesc._wcid == 13235 || // W_HOUSEVILLATEST10001_CLASS
                           parsed.wdesc._wcid >= 13243 && parsed.wdesc._wcid <= 14042 || // W_HOUSECOTTAGE1451_CLASS to W_HOUSECOTTAGE2350_CLASS
                           parsed.wdesc._wcid >= 14043 && parsed.wdesc._wcid <= 14222 || // W_HOUSEVILLA1851_CLASS to W_HOUSEVILLA2440_CLASS
                           parsed.wdesc._wcid >= 14223 && parsed.wdesc._wcid <= 14242 || // W_HOUSEMANSION1941_CLASS to W_HOUSEMANSION2450_CLASS
                           parsed.wdesc._wcid >= 14938 && parsed.wdesc._wcid <= 15087 || // W_HOUSECOTTAGE2451_CLASS to W_HOUSECOTTAGE2600_CLASS
                           parsed.wdesc._wcid >= 15088 && parsed.wdesc._wcid <= 15127 || // W_HOUSEVILLA2601_CLASS to W_HOUSEVILLA2640_CLASS
                           parsed.wdesc._wcid >= 15128 && parsed.wdesc._wcid <= 15137 || // W_HOUSEMANSION2641_CLASS to W_HOUSEMANSION2650_CLASS
                           parsed.wdesc._wcid >= 15452 && parsed.wdesc._wcid <= 15457 || // W_HOUSEAPARTMENT2851_CLASS to W_HOUSEAPARTMENT2856_CLASS
                           parsed.wdesc._wcid >= 15458 && parsed.wdesc._wcid <= 15607 || // W_HOUSECOTTAGE2651_CLASS to W_HOUSECOTTAGE2800_CLASS
                           parsed.wdesc._wcid >= 15612 && parsed.wdesc._wcid <= 15661 || // W_HOUSEVILLA2801_CLASS to W_HOUSEVILLA2850_CLASS
                           parsed.wdesc._wcid >= 15897 && parsed.wdesc._wcid <= 16890 || // W_HOUSEAPARTMENT2857_CLASS to W_HOUSEAPARTMENT3850_CLASS
                           parsed.wdesc._wcid >= 16923 && parsed.wdesc._wcid <= 18923 || // W_HOUSEAPARTMENT4051_CLASS to W_HOUSEAPARTMENT6050_CLASS
                           parsed.wdesc._wcid >= 18924 && parsed.wdesc._wcid <= 19073 || // W_HOUSECOTTAGE3851_CLASS to W_HOUSECOTTAGE4000_CLASS
                           parsed.wdesc._wcid >= 19077 && parsed.wdesc._wcid <= 19126 || // W_HOUSEVILLA4001_CLASS to W_HOUSEVILLA4050_CLASS
                           parsed.wdesc._wcid >= 20650 && parsed.wdesc._wcid <= 20799 || // W_HOUSECOTTAGE6051_CLASS to W_HOUSECOTTAGE6200_CLASS
                           parsed.wdesc._wcid >= 20800 && parsed.wdesc._wcid <= 20839 || // W_HOUSEVILLA6201_CLASS to W_HOUSEVILLA6240_CLASS
                           parsed.wdesc._wcid >= 20840 && parsed.wdesc._wcid <= 20849    // W_HOUSEMANSION6241_CLASS to W_HOUSEMANSION6250_CLASS
                           )
                    {
                        fileToPutItIn = "HouseObjects";
                        weenieType = WeenieType.House_WeenieType;
                        addIt = true;
                    }
                    //else if (parsed.wdesc._name.m_buffer.Contains("Essence"))
                    //{
                    //    weeniefileToPutItIn = "Essences";
                    //    addWeenie = true;
                    //}
                    //else if (parsed.wdesc._name.m_buffer.Contains("Spirit"))
                    //{
                    //    weeniefileToPutItIn = "Spirits";
                    //    addWeenie = true;
                    //}
                    else if (parsed.physicsdesc.setup_id == 33555088
                        || parsed.physicsdesc.setup_id == 33557390
                        || parsed.physicsdesc.setup_id == 33555594
                        || parsed.physicsdesc.setup_id == 33555909
                        )
                    {
                        fileToPutItIn = "MiscBuildingSigns";
                        weenieType = WeenieType.Generic_WeenieType;
                        addIt = true;
                    }
                    else if (parsed.wdesc._name.m_buffer.Contains("Residential Halls"))
                    {
                        fileToPutItIn = "MiscResidentialHallSigns";
                        weenieType = WeenieType.Generic_WeenieType;
                        addIt = true;
                    }
                    else if (parsed.wdesc._name.m_buffer.Contains("Deed"))
                    {
                        fileToPutItIn = "HouseDeeds";
                        weenieType = WeenieType.Deed_WeenieType;
                        addWeenie = true;
                    }
                    else if (parsed.wdesc._name.m_buffer.Contains("Court")
                        || parsed.wdesc._name.m_buffer.Contains("Dwellings")
                        || parsed.wdesc._name.m_buffer.Contains("SylvanDwellings")
                        || parsed.wdesc._name.m_buffer.Contains("Veranda")
                        || parsed.wdesc._name.m_buffer.Contains("Gate")
                        || (parsed.wdesc._name.m_buffer.Contains("Yard") && !parsed.wdesc._name.m_buffer.Contains("Balloons"))
                        || parsed.wdesc._name.m_buffer.Contains("Gardens")
                        || parsed.wdesc._name.m_buffer.Contains("Lodge")
                        || parsed.wdesc._name.m_buffer.Contains("Grotto")
                        || (parsed.wdesc._name.m_buffer.Contains("Hollow") && !parsed.wdesc._name.m_buffer.Contains("Minion"))
                        )
                    {
                        fileToPutItIn = "MiscResidentialHallSigns";
                        weenieType = WeenieType.Generic_WeenieType;
                        addIt = true;
                    }
                    else if (parsed.wdesc._name.m_buffer.Contains("Festival Stone"))
                    {
                        fileToPutItIn = "MiscFestivalStones";
                        weenieType = WeenieType.Generic_WeenieType;
                        addIt = true;
                    }
                    else if (parsed.physicsdesc.setup_id == 33557463
                        )
                    {
                        fileToPutItIn = "MiscSettlementMarkers";
                        weenieType = WeenieType.Generic_WeenieType;
                        parsed.physicsdesc.bitfield = 32769; // error correct
                        addIt = true;
                    }
                    else if (parsed.wdesc._name.m_buffer.Contains("Button"))
                    {
                        fileToPutItIn = "MiscButtons";
                        weenieType = WeenieType.Switch_WeenieType;
                        addIt = true;
                    }
                    else if (parsed.wdesc._name.m_buffer.Contains("Lever")
                        || parsed.wdesc._name.m_buffer.Contains("Candle") && !parsed.wdesc._name.m_buffer.Contains("Floating") && !parsed.wdesc._name.m_buffer.Contains("Bronze")
                        || parsed.wdesc._name.m_buffer.Contains("Torch") && parsed.wdesc._wcid != 293
                        || parsed.wdesc._name.m_buffer.Contains("Plant") && !parsed.wdesc._name.m_buffer.Contains("Fertilized")
                        )
                    {
                        fileToPutItIn = "MiscLevers";
                        weenieType = WeenieType.Switch_WeenieType;
                        addIt = true;
                    }
                    else if (parsed.object_id < 0x80000000)
                    {
                        fileToPutItIn = "MiscStaticsObjects";
                        weenieType = WeenieType.Generic_WeenieType;
                        addIt = true;
                    }
                    else
                    {
                        fileToPutItIn = "MiscObjects";
                        weenieType = WeenieType.Generic_WeenieType;
                        addWeenie = true;
                    }
                }
                else if (parsed.wdesc._type == ITEM_TYPE.TYPE_PORTAL) // HOUSE PORTALS
                {
                    if (
                        parsed.wdesc._wcid == 9620 || // W_PORTALHOUSE_CLASS
                        parsed.wdesc._wcid == 10751 || // W_PORTALHOUSETEST_CLASS
                        parsed.wdesc._wcid == 11730    // W_HOUSEPORTAL_CLASS
                                            )
                    {
                        fileToPutItIn = "HousePortals";
                        weenieType = WeenieType.HousePortal_WeenieType;
                        parsed.wdesc._bitfield = 262164; // error correct
                        parsed.wdesc.header = 41943088; // error correct
                        addIt = true;
                    }
                    else if (parsed.wdesc._wcid == 1955)
                    {
                        fileToPutItIn = "PortalsSummoned";
                        weenieType = WeenieType.Portal_WeenieType;
                        addWeenie = true;
                    }
                    else if (parsed.wdesc._name.m_buffer.Contains("Town Network"))
                    {
                        fileToPutItIn = "PortalsTownNetwork";
                        weenieType = WeenieType.Portal_WeenieType;
                        addIt = true;
                    }
                    else if (parsed.wdesc._name.m_buffer.Contains("Floating City"))
                    {
                        fileToPutItIn = "PortalsFloatingCity";
                        weenieType = WeenieType.Portal_WeenieType;
                        addIt = true;
                    }
                    else if (parsed.wdesc._name.m_buffer.Contains("Humming Crystal"))
                    {
                        fileToPutItIn = "PortalsHummingCrystal";
                        weenieType = WeenieType.Portal_WeenieType;
                        addIt = true;
                    }
                    else if (parsed.wdesc._name.m_buffer.Contains("The Orphanage"))
                    {
                        fileToPutItIn = "PortalsTheOrphanage";
                        weenieType = WeenieType.Portal_WeenieType;
                        addIt = true;
                    }
                    else if (parsed.wdesc._name.m_buffer.Contains("Golem Sanctum"))
                    {
                        fileToPutItIn = "PortalsGolemSanctum";
                        weenieType = WeenieType.Portal_WeenieType;
                        addIt = true;
                    }
                    else if (parsed.wdesc._name.m_buffer.Contains("Destroyed"))
                    {
                        fileToPutItIn = "PortalsDestroyed";
                        weenieType = WeenieType.Portal_WeenieType;
                        addIt = true;
                    }
                    else if (parsed.wdesc._name.m_buffer.Contains("Meeting Hall"))
                    {
                        fileToPutItIn = "PortalsMeetingHall";
                        weenieType = WeenieType.Portal_WeenieType;
                        addIt = true;
                    }
                    else if (parsed.wdesc._name.m_buffer.Contains("Portal to"))
                    {
                        fileToPutItIn = "PortalsPortalto";
                        weenieType = WeenieType.Portal_WeenieType;
                        addIt = true;
                    }
                    else if (parsed.wdesc._name.m_buffer.Contains("Portal"))
                    {
                        fileToPutItIn = "PortalsPortal";
                        weenieType = WeenieType.Portal_WeenieType;
                        addIt = true;
                    }
                    else
                    {
                        fileToPutItIn = "Portals";
                        weenieType = WeenieType.Portal_WeenieType;
                        addIt = true;
                    }
                }
                else if (parsed.wdesc._type == ITEM_TYPE.TYPE_CONTAINER) // HOOKS AND STORAGE
                {
                    ////if (
                    ////    parsed.wdesc._wcid == 9686 && parsed.wdesc._name.m_buffer.Contains("Hook") || // W_HOOK_CLASS
                    ////    parsed.wdesc._wcid == 11697 && parsed.wdesc._name.m_buffer.Contains("Hook") || // W_HOOK_FLOOR_CLASS
                    ////    parsed.wdesc._wcid == 11698 && parsed.wdesc._name.m_buffer.Contains("Hook") || // W_HOOK_CEILING_CLASS
                    ////    parsed.wdesc._wcid == 12678 && parsed.wdesc._name.m_buffer.Contains("Hook") || // W_HOOK_ROOF_CLASS
                    ////    parsed.wdesc._wcid == 12679 && parsed.wdesc._name.m_buffer.Contains("Hook") // W_HOOK_YARD_CLASS
                    ////    )
                    ////{
                    ////    fileToPutItIn = "HouseHooks";
                    ////    weenieType = WeenieType.Hook_WeenieType;
                    ////    addIt = true;

                    ////    foreach (var weenie in weenies[fileToPutItIn])
                    ////    {
                    ////        if ((weenie.wdesc._wcid == parsed.wdesc._wcid) && !weenie.wdesc._name.m_buffer.Contains("Hook") 
                    ////            && !parsed.wdesc._name.m_buffer.Contains("Pirate")
                    ////             && !parsed.wdesc._name.m_buffer.Contains("Healing Machine"))
                    ////        {
                    ////            weenies[fileToPutItIn].Remove(weenie);
                    ////            weenies[fileToPutItIn].Add(parsed);
                    ////            break;
                    ////        }
                    ////    }
                    ////}
                    ////else if (
                    ////        parsed.wdesc._wcid == 9686 || // W_HOOK_CLASS
                    ////        parsed.wdesc._wcid == 11697 || // W_HOOK_FLOOR_CLASS
                    ////        parsed.wdesc._wcid == 11698 || // W_HOOK_CEILING_CLASS
                    ////        parsed.wdesc._wcid == 12678 || // W_HOOK_ROOF_CLASS
                    ////        parsed.wdesc._wcid == 12679  // W_HOOK_YARD_CLASS
                    ////        )
                    ////{
                    ////    fileToPutItIn = "HouseHooks";
                    ////    weenieType = WeenieType.Hook_WeenieType;
                    ////    //if (!addEverything)
                    ////    //{
                    ////    //    if (parsed.wdesc._wcid == 9686)
                    ////    //        parsed.wdesc._name.m_buffer = "Wall Hook";
                    ////    //    if (parsed.wdesc._wcid == 11697)
                    ////    //        parsed.wdesc._name.m_buffer = "Floor Hook";
                    ////    //    if (parsed.wdesc._wcid == 11698)
                    ////    //        parsed.wdesc._name.m_buffer = "Ceiling Hook";
                    ////    //    if (parsed.wdesc._wcid == 12678)
                    ////    //        parsed.wdesc._name.m_buffer = "Roof Hook";
                    ////    //    if (parsed.wdesc._wcid == 12679)
                    ////    //        parsed.wdesc._name.m_buffer = "Yard Hook";
                    ////    //}
                    ////    addWeenie = true;
                    ////}

                    if (
                        parsed.wdesc._wcid == 9686 || // W_HOOK_CLASS
                        parsed.wdesc._wcid == 11697 || // W_HOOK_FLOOR_CLASS
                        parsed.wdesc._wcid == 11698 || // W_HOOK_CEILING_CLASS
                        parsed.wdesc._wcid == 12678 || // W_HOOK_ROOF_CLASS
                        parsed.wdesc._wcid == 12679 // W_HOOK_YARD_CLASS
                        )
                    {
                        fileToPutItIn = "HouseHooks";
                        weenieType = WeenieType.Hook_WeenieType;
                        addIt = true;

                        if (weenies.ContainsKey(fileToPutItIn))
                        {
                            foreach (var weenie in weenies[fileToPutItIn])
                            {
                                if ((weenie.wdesc._wcid == parsed.wdesc._wcid) && !weenie.wdesc._name.m_buffer.Contains("Hook")
                                    && !parsed.wdesc._name.m_buffer.Contains("Pirate")
                                     && !parsed.wdesc._name.m_buffer.Contains("Healing Machine"))
                                {
                                    weenies[fileToPutItIn].Remove(weenie);
                                    weenies[fileToPutItIn].Add(parsed);
                                    break;
                                }
                            }
                        }
                    }
                    else if (
                            parsed.wdesc._wcid == 9687     // W_STORAGE_CLASS
                            )
                    {
                        fileToPutItIn = "HouseStorage";
                        weenieType = WeenieType.Storage_WeenieType;
                        addIt = true;
                    }
                    else if (parsed.wdesc._wcid == 21)
                    {
                        fileToPutItIn = "ContainersCorpses";
                        weenieType = WeenieType.Corpse_WeenieType;
                        addWeenie = true;
                    }
                    //else if (parsed.wdesc._name.m_buffer.Contains("Corpse"))
                    //{
                    //    fileToPutItIn = "Corpses";
                    //    addIt = true;
                    //}
                    else if (parsed.wdesc._name.m_buffer.Contains("Standing Stone"))
                    {
                        fileToPutItIn = "ContainersStandingStones";
                        weenieType = WeenieType.Container_WeenieType;
                        addIt = true;
                    }
                    else if (parsed.wdesc._name.m_buffer.Contains("Pack")
                        || parsed.wdesc._name.m_buffer.Contains("Backpack")
                        || parsed.wdesc._name.m_buffer.Contains("Sack")
                        || parsed.wdesc._name.m_buffer.Contains("Pouch")
                        )
                    {
                        fileToPutItIn = "ContainersPacks";
                        weenieType = WeenieType.Container_WeenieType;
                        addWeenie = true;
                    }
                    else if (
                        parsed.wdesc._name.m_buffer.Contains("Chest")
                        || parsed.wdesc._name.m_buffer.Contains("Coffer")
                        || parsed.wdesc._name.m_buffer.Contains("Vault")
                        || parsed.wdesc._name.m_buffer.Contains("Storage")
                        || parsed.wdesc._name.m_buffer.Contains("Stump")
                        || parsed.wdesc._name.m_buffer.Contains("Shelf")
                        || parsed.wdesc._name.m_buffer.Contains("Reliquary")
                        || parsed.wdesc._name.m_buffer.Contains("Crate")
                        || parsed.wdesc._name.m_buffer.Contains("Cache")
                        || parsed.wdesc._name.m_buffer.Contains("Tomb")
                        || parsed.wdesc._name.m_buffer.Contains("Sarcophagus")
                        || parsed.wdesc._name.m_buffer.Contains("Footlocker")
                        || parsed.wdesc._name.m_buffer.Contains("Holding")
                        || parsed.wdesc._name.m_buffer.Contains("Wheelbarrow")
                        || parsed.wdesc._name.m_buffer.Contains("Stash")
                        || parsed.wdesc._name.m_buffer.Contains("Trove")
                        || parsed.wdesc._name.m_buffer.Contains("Prism")
                        || parsed.wdesc._name.m_buffer.Contains("Strongbox")
                        || parsed.wdesc._name.m_buffer.Contains("Supplies")
                        )
                    {
                        fileToPutItIn = "ContainersChests";
                        weenieType = WeenieType.Chest_WeenieType;
                        addIt = true;
                    }
                    else if (parsed.object_id < 0x80000000)
                    {
                        fileToPutItIn = "ContainersStatics";
                        weenieType = WeenieType.Container_WeenieType;
                        addIt = true;
                    }
                    else
                    {
                        fileToPutItIn = "Containers";
                        weenieType = WeenieType.Container_WeenieType;
                        addWeenie = true;
                    }
                }
                else if (parsed.wdesc._type == ITEM_TYPE.TYPE_UNDEF) // SLUMLORD OBJECTS
                {
                    if (
                        parsed.wdesc._wcid == 9621 || // W_SLUMLORD_CLASS
                        parsed.wdesc._wcid == 10752 || // W_SLUMLORDTESTCHEAP_CLASS
                        parsed.wdesc._wcid == 10753 || // W_SLUMLORDTESTEXPENSIVE_CLASS
                        parsed.wdesc._wcid == 10754 || // W_SLUMLORDTESTMODERATE_CLASS
                        parsed.wdesc._wcid == 11711 || // W_SLUMLORDCOTTAGECHEAP_CLASS
                        parsed.wdesc._wcid == 11712 || // W_SLUMLORDCOTTAGEEXPENSIVE_CLASS
                        parsed.wdesc._wcid == 11713 || // W_SLUMLORDCOTTAGEMODERATE_CLASS
                        parsed.wdesc._wcid == 11714 || // W_SLUMLORDMANSIONCHEAP_CLASS
                        parsed.wdesc._wcid == 11715 || // W_SLUMLORDMANSIONEXPENSIVE_CLASS
                        parsed.wdesc._wcid == 11716 || // W_SLUMLORDMANSIONMODERATE_CLASS
                        parsed.wdesc._wcid == 11717 || // W_SLUMLORDVILLACHEAP_CLASS
                        parsed.wdesc._wcid == 11718 || // W_SLUMLORDVILLAEXPENSIVE_CLASS
                        parsed.wdesc._wcid == 11719 || // W_SLUMLORDVILLAMODERATE_CLASS
                        parsed.wdesc._wcid == 11977 || // W_SLUMLORDCOTTAGES349_579_CLASS
                        parsed.wdesc._wcid == 11978 || // W_SLUMLORDVILLA851_925_CLASS
                        parsed.wdesc._wcid == 11979 || // W_SLUMLORDCOTTAGE580_800_CLASS
                        parsed.wdesc._wcid == 11980 || // W_SLUMLORDVILLA926_970_CLASS
                        parsed.wdesc._wcid == 11980 || // W_SLUMLORDVILLA926_970_CLASS
                        parsed.wdesc._wcid == 12461 || // W_SLUMLORDCOTTAGE1001_1075_CLASS
                        parsed.wdesc._wcid == 12462 || // W_SLUMLORDCOTTAGE1076_1150_CLASS
                        parsed.wdesc._wcid == 13078 || // W_SLUMLORDCOTTAGE1151_1275_CLASS
                        parsed.wdesc._wcid == 13079 || // W_SLUMLORDCOTTAGE1276_1400_CLASS
                        parsed.wdesc._wcid == 13080 || // W_SLUMLORDVILLA1401_1440_CLASS
                        parsed.wdesc._wcid == 13081 || // W_SLUMLORDMANSION1441_1450_CLASS
                        parsed.wdesc._wcid == 14243 || // W_SLUMLORDCOTTAGE1451_1650_CLASS
                        parsed.wdesc._wcid == 14244 || // W_SLUMLORDCOTTAGE1651_1850_CLASS
                        parsed.wdesc._wcid == 14245 || // W_SLUMLORDVILLA1851_1940_CLASS
                        parsed.wdesc._wcid == 14246 || // W_SLUMLORDMANSION1941_1950_CLASS
                        parsed.wdesc._wcid == 14247 || // W_SLUMLORDCOTTAGE1951_2150_CLASS
                        parsed.wdesc._wcid == 14248 || // W_SLUMLORDCOTTAGE2151_2350_CLASS
                        parsed.wdesc._wcid == 14249 || // W_SLUMLORDVILLA2351_2440_CLASS
                        parsed.wdesc._wcid == 14250 || // W_SLUMLORDMANSION2441_2450_CLASS
                        parsed.wdesc._wcid == 14934 || // W_SLUMLORDCOTTAGE2451_2525_CLASS
                        parsed.wdesc._wcid == 14935 || // W_SLUMLORDCOTTAGE2526_2600_CLASS
                        parsed.wdesc._wcid == 14936 || // W_SLUMLORDVILLA2601_2640_CLASS
                        parsed.wdesc._wcid == 14937 || // W_SLUMLORDMANSION2641_2650_CLASS
                                                       // parsed.wdesc._wcid == 15273 || // W_SLUMLORDFAKENUHMUDIRA_CLASS
                        parsed.wdesc._wcid == 15608 || // W_SLUMLORDAPARTMENT_CLASS
                        parsed.wdesc._wcid == 15609 || // W_SLUMLORDCOTTAGE2651_2725_CLASS
                        parsed.wdesc._wcid == 15610 || // W_SLUMLORDCOTTAGE2726_2800_CLASS
                        parsed.wdesc._wcid == 15611 || // W_SLUMLORDVILLA2801_2850_CLASS
                        parsed.wdesc._wcid == 19074 || // W_SLUMLORDCOTTAGE3851_3925_CLASS
                        parsed.wdesc._wcid == 19075 || // W_SLUMLORDCOTTAGE3926_4000_CLASS
                        parsed.wdesc._wcid == 19076 || // W_SLUMLORDVILLA4001_4050_CLASS
                        parsed.wdesc._wcid == 20850 || // W_SLUMLORDCOTTAGE6051_6125_CLASS
                        parsed.wdesc._wcid == 20851 || // W_SLUMLORDCOTTAGE6126_6200_CLASS
                        parsed.wdesc._wcid == 20852 || // W_SLUMLORDVILLA6201_6240_CLASS
                        parsed.wdesc._wcid == 20853    // W_SLUMLORDMANSION6241_6250_CLASS
                                                       // parsed.wdesc._wcid == 22118 || // W_SLUMLORDHAUNTEDMANSION_CLASS
                        )
                    {
                        fileToPutItIn = "HouseSlumLords";
                        weenieType = WeenieType.SlumLord_WeenieType;

                        if (!addEverything)
                        {
                            if (parsed.wdesc._name.m_buffer.Contains("'s Cottage"))
                                parsed.wdesc._name.m_buffer = "Cottage";
                            if (parsed.wdesc._name.m_buffer.Contains("'s Villa"))
                                parsed.wdesc._name.m_buffer = "Villa";
                            if (parsed.wdesc._name.m_buffer.Contains("'s Mansion"))
                                parsed.wdesc._name.m_buffer = "Mansion";
                            if (parsed.wdesc._name.m_buffer.Contains("'s Apartment"))
                                parsed.wdesc._name.m_buffer = "Apartment";
                        }

                        parsed.wdesc.header = 33554480; // error correct
                        parsed.physicsdesc.bitfield = 98435; // error correct

                        addIt = true;
                    }

                    else if (
                                                        parsed.wdesc._wcid == 15273 || // W_SLUMLORDFAKENUHMUDIRA_CLASS
                                                        parsed.wdesc._wcid == 22118    // W_SLUMLORDHAUNTEDMANSION_CLASS
                        )
                    {
                        fileToPutItIn = "FakeSlumLords";
                        weenieType = WeenieType.SlumLord_WeenieType;
                        addIt = true;
                    }
                    else if (parsed.wdesc._wcid == 10762)
                    {
                        fileToPutItIn = "HousePortalLinkspots";
                        weenieType = WeenieType.Generic_WeenieType;
                        addIt = true;
                    }
                    else if (parsed.wdesc._name.m_buffer.Contains("Gen")
                        )
                    {
                        // fileToPutItIn = "Generators";
                        // addIt = true;
                        fileToPutItIn = "Generators";
                        weenieType = WeenieType.Generic_WeenieType;
                        addWeenie = true;
                    }
                    //else if (
                    //    parsed.wdesc._name.m_buffer.Contains("Bolt")
                    //    || parsed.wdesc._name.m_buffer.Contains("wave")
                    //    || parsed.wdesc._name.m_buffer.Contains("Wave")
                    //    || parsed.wdesc._name.m_buffer.Contains("Blast")
                    //    || parsed.wdesc._name.m_buffer.Contains("Ring")
                    //    || parsed.wdesc._name.m_buffer.Contains("Stream")
                    //    || parsed.wdesc._name.m_buffer.Contains("Fist")
                    //    || parsed.wdesc._name.m_buffer.Contains("Missile")
                    //    || parsed.wdesc._name.m_buffer.Contains("Egg")
                    //    || parsed.wdesc._name.m_buffer.Contains("Death")
                    //    || parsed.wdesc._name.m_buffer.Contains("Fury")
                    //     || parsed.wdesc._name.m_buffer.Contains("Wind")
                    //    || parsed.wdesc._name.m_buffer.Contains("Flaming Skull")
                    //     || parsed.wdesc._name.m_buffer.Contains("Edge")
                    //    || parsed.wdesc._name.m_buffer.Contains("Snowball")
                    //    || parsed.wdesc._name.m_buffer.Contains("Bomb")
                    //    || parsed.wdesc._name.m_buffer.Contains("Blade")
                    //    || parsed.wdesc._name.m_buffer.Contains("Stalactite")
                    //    || parsed.wdesc._name.m_buffer.Contains("Boulder")
                    //    || parsed.wdesc._name.m_buffer.Contains("Whirlwind")
                    //    )
                    //{
                    //    weeniefileToPutItIn = "UndefObjects";
                    //    addWeenie = true;
                    //}
                    else if (parsed.wdesc._name.m_buffer.Contains("Generator"))
                    {
                        // fileToPutItIn = "Generators";
                        // addIt = true;
                        fileToPutItIn = "Generators";
                        weenieType = WeenieType.Generic_WeenieType;
                        addWeenie = true;
                    }
                    //else if (parsed.wdesc._name.m_buffer.Contains("Rabbit"))
                    //{
                    //    fileToPutItIn = "UndefRabbits";
                    //    addIt = true;
                    //}
                    else if (
                           parsed.wdesc._name.m_buffer.Contains("Bolt")
                        || parsed.wdesc._name.m_buffer.Contains("wave")
                        || parsed.wdesc._name.m_buffer.Contains("Wave")
                        || parsed.wdesc._name.m_buffer.Contains("Blast")
                        || parsed.wdesc._name.m_buffer.Contains("Ring")
                        || parsed.wdesc._name.m_buffer.Contains("Stream")
                        || parsed.wdesc._name.m_buffer.Contains("Fist")
                        // || parsed.wdesc._name.m_buffer.Contains("Missile")
                        // || parsed.wdesc._name.m_buffer.Contains("Egg")
                        || parsed.wdesc._name.m_buffer.Contains("Death")
                        || parsed.wdesc._name.m_buffer.Contains("Fury")
                         || parsed.wdesc._name.m_buffer.Contains("Wind")
                        || parsed.wdesc._name.m_buffer.Contains("Flaming Skull")
                         || parsed.wdesc._name.m_buffer.Contains("Edge")
                        // || parsed.wdesc._name.m_buffer.Contains("Snowball")
                        || parsed.wdesc._name.m_buffer.Contains("Bomb")
                        || parsed.wdesc._name.m_buffer.Contains("Blade")
                        || parsed.wdesc._name.m_buffer.Contains("Stalactite")
                        || parsed.wdesc._name.m_buffer.Contains("Boulder")
                        || parsed.wdesc._name.m_buffer.Contains("Whirlwind")
                    )
                    {
                        fileToPutItIn = "ProjectileSpellObjects";
                        weenieType = WeenieType.ProjectileSpell_WeenieType;
                        addWeenie = true;
                    }
                    else if (parsed.wdesc._name.m_buffer.Contains("Missile")
                            || parsed.wdesc._name.m_buffer.Contains("Egg")
                            || parsed.wdesc._name.m_buffer.Contains("Snowball")
                    )
                    {
                        fileToPutItIn = "MissileObjects";
                        weenieType = WeenieType.Missile_WeenieType;
                        addWeenie = true;
                    }
                    else if (parsed.object_id < 0x80000000)
                    {
                        fileToPutItIn = "UndefStatics";
                        weenieType = WeenieType.Generic_WeenieType;
                        addIt = true;
                    }
                    else
                    {
                        fileToPutItIn = "UndefObjects";
                        weenieType = WeenieType.Generic_WeenieType;
                        addWeenie = true;
                    }
                }
                else if (parsed.wdesc._type == ITEM_TYPE.TYPE_WRITABLE)
                {
                    if (parsed.wdesc._name.m_buffer.Contains("Statue"))
                    {
                        fileToPutItIn = "WriteablesStatues";
                        weenieType = WeenieType.Book_WeenieType;
                        addIt = true;
                    }
                    else if (parsed.wdesc._name.m_buffer.Contains("Scroll")
                        || parsed.wdesc._name.m_buffer.Contains("Aura")
                        || parsed.wdesc._name.m_buffer.Contains("Recall")
                        || parsed.wdesc._name.m_buffer.Contains("Inscription")
                        )
                    {
                        fileToPutItIn = "WriteablesScrolls";
                        weenieType = WeenieType.Scroll_WeenieType;
                        addWeenie = true;
                    }
                    else if (parsed.wdesc._name.m_buffer.Contains("Pack"))
                    {
                        fileToPutItIn = "WriteablesPackToys";
                        weenieType = WeenieType.Book_WeenieType;
                        addWeenie = true;
                    }
                    else if (parsed.wdesc._wcid == 9002)
                    {
                        fileToPutItIn = "WriteablesShardVigil";
                        weenieType = WeenieType.Book_WeenieType;
                        addIt = true;
                    }
                    else if (parsed.object_id < 0x80000000)
                    {
                        fileToPutItIn = "WritableStaticsObjects";
                        weenieType = WeenieType.Book_WeenieType;
                        addIt = true;
                    }
                    else
                    {
                        fileToPutItIn = "WritableObjects";
                        weenieType = WeenieType.Book_WeenieType;
                        addWeenie = true;
                    }
                }
                else if (parsed.wdesc._type == ITEM_TYPE.TYPE_LIFESTONE)
                {
                    fileToPutItIn = "Lifestones";
                    weenieType = WeenieType.LifeStone_WeenieType;
                    addIt = true;
                }
                ////else if ((parsed.wdesc._name.m_buffer.Contains("Scrivener")
                ////        || parsed.wdesc._name.m_buffer.Contains("Scribe")
                ////        || parsed.wdesc._name.m_buffer.Contains("Archmage")
                ////        || parsed.wdesc._name.m_buffer.Contains("Healer")
                ////        || parsed.wdesc._name.m_buffer.Contains("Weaponsmith")
                ////        || parsed.wdesc._name.m_buffer.Contains("Weapons Master")
                ////        || parsed.wdesc._name.m_buffer.Contains("Armorer")
                ////        || parsed.wdesc._name.m_buffer.Contains("Grocer")
                ////        || parsed.wdesc._name.m_buffer.Contains("Shopkeep")
                ////        || parsed.wdesc._name.m_buffer.Contains("Shopkeeper")
                ////        || parsed.wdesc._name.m_buffer.Contains("Jeweler")
                ////        || parsed.wdesc._name.m_buffer.Contains("Barkeep")
                ////        || parsed.wdesc._name.m_buffer.Contains("Barkeeper")
                ////        || parsed.wdesc._name.m_buffer.Contains("Provisioner")
                ////        || parsed.wdesc._name.m_buffer.Contains("Tailor")
                ////        || parsed.wdesc._name.m_buffer.Contains("Seamstress")
                ////        || parsed.wdesc._name.m_buffer.Contains("Fletcher")
                ////        || parsed.wdesc._name.m_buffer.Contains("Bowyer")
                ////        || parsed.wdesc._name.m_buffer.Contains("Marksman")
                ////        || parsed.wdesc._name.m_buffer.Contains("Crafter")
                ////        || parsed.wdesc._name.m_buffer.Contains("Cook")
                ////        || parsed.wdesc._name.m_buffer.Contains("Alchemist")
                ////        || parsed.wdesc._name.m_buffer.Contains("Woodsman")
                ////        || parsed.wdesc._name.m_buffer.Contains("Apprentice"))
                ////    && parsed.wdesc._type == ITEM_TYPE.TYPE_CREATURE
                ////    && parsed.wdesc._blipColor == 8)
                ////{
                ////    fileToPutItIn = "Vendors";
                ////    addIt = true;
                ////}
                ////else if ((parsed.wdesc._name.m_buffer == "Agent of the Arcanum"
                ////        || parsed.wdesc._name.m_buffer == "Sentry"
                ////        || parsed.wdesc._name.m_buffer == "Ulgrim the Unpleasant"
                ////        || parsed.wdesc._name.m_buffer.Contains("Ulgrim")
                ////        || parsed.wdesc._name.m_buffer == "Ned the Clever"
                ////        || parsed.wdesc._name.m_buffer == "Wedding Planner"
                ////        || parsed.wdesc._name.m_buffer.Contains("Collector")
                ////        || parsed.wdesc._name.m_buffer.Contains("Guard")
                ////        || parsed.wdesc._name.m_buffer == "Jonathan"
                ////        || parsed.wdesc._name.m_buffer == "Farmer")
                ////    && parsed.wdesc._type == ITEM_TYPE.TYPE_CREATURE
                ////    && parsed.wdesc._blipColor == 8)
                ////{
                ////    fileToPutItIn = "OtherNPCs";
                ////    addIt = true;
                ////}
                else if (parsed.wdesc._type == ITEM_TYPE.TYPE_CREATURE)
                {
                    if (//parsed.wdesc._name.m_buffer == "The Chicken"
                        //|| parsed.wdesc._name.m_buffer == "Babe the Blue Auroch"
                        parsed.wdesc._name.m_buffer == "Babe the Blue Auroch"
                        || parsed.wdesc._name.m_buffer == "Paul the Monouga"
                        //|| parsed.wdesc._name.m_buffer == "Silencia's Magma Golem"
                        //|| parsed.wdesc._name.m_buffer == "Repair Golem"
                        )
                    {
                        fileToPutItIn = "CreaturesSpecialNPCs";
                        weenieType = WeenieType.Creature_WeenieType;
                        addIt = true;
                        margin = 15f;
                    }
                    else if (parsed.wdesc._wcid == 43481
                        || parsed.wdesc._wcid == 43480
                        )
                    {
                        fileToPutItIn = "CreaturesOlthoiPlayers";
                        weenieType = WeenieType.Creature_WeenieType;
                        addWeenie = true;
                    }
                    else if (parsed.wdesc._name.m_buffer.Contains("Crier")
                        && parsed.wdesc._blipColor == 8)
                    {
                        fileToPutItIn = "CreaturesTownCriers";
                        weenieType = WeenieType.Creature_WeenieType;
                        addIt = true;
                        margin = 20f;
                    }
                    else if (parsed.wdesc._name.m_buffer.Contains("Pet")
                        || parsed.wdesc._name.m_buffer.Contains("Wind-up")
                        || parsed.wdesc._wcid == 48881
                        || parsed.wdesc._wcid == 34902
                        || parsed.wdesc._wcid == 48891
                        || parsed.wdesc._wcid == 48879
                        || parsed.wdesc._wcid == 34906
                        || parsed.wdesc._wcid == 48887
                        || parsed.wdesc._wcid == 48889
                        || parsed.wdesc._wcid == 48883
                        || parsed.wdesc._wcid == 34900
                        || parsed.wdesc._wcid == 34901
                        || parsed.wdesc._wcid == 34908
                        || parsed.wdesc._wcid == 34898
                        )
                    {
                        fileToPutItIn = "CreaturesPets";
                        weenieType = WeenieType.Pet_WeenieType;
                        addWeenie = true;
                    }
                    else if (parsed.wdesc._blipColor == 8)
                    {
                        fileToPutItIn = "CreaturesNPCs";
                        weenieType = WeenieType.Creature_WeenieType;
                        addIt = true;
                        margin = 15f;
                    }
                    else if (parsed.wdesc._blipColor == 2)
                    {
                        fileToPutItIn = "CreaturesMonsters";
                        weenieType = WeenieType.Creature_WeenieType;
                        addWeenie = true;
                    }
                    else if (parsed.object_id < 0x80000000)
                    {
                        fileToPutItIn = "CreaturesNPCStatics";
                        weenieType = WeenieType.Creature_WeenieType;
                        addIt = true;
                    }
                    else if (parsed.wdesc._name.m_buffer.Contains("Statue") && !parsed.wdesc._name.m_buffer.Contains("Bronze")
                        || parsed.wdesc._name.m_buffer.Contains("Shrine")
                        // || parsed.wdesc._name.m_buffer.Contains("Altar")
                        || parsed.wdesc._name.m_buffer.Contains("Warden of")
                        || parsed.wdesc._name.m_buffer.Contains("Device")
                        || parsed.wdesc._name.m_buffer.Contains("Seed")
                        || parsed.wdesc._name.m_buffer.Contains("Forge")
                        || parsed.wdesc._name.m_buffer.Contains("Tower Guardian")
                        || parsed.wdesc._name.m_buffer.Contains("New Aluvian Champion")
                        || parsed.wdesc._name.m_buffer.Contains("Barrel")
                        || parsed.wdesc._name.m_buffer.Contains("New Aluvian War Mage Champion")
                        || parsed.wdesc._name.m_buffer.Contains("Wounded Drudge Skulker")
                        || parsed.wdesc._name.m_buffer.Contains("Servant of")
                        || parsed.wdesc._name.m_buffer.Contains("Prison")
                        || parsed.wdesc._name.m_buffer.Contains("Temple")
                        || parsed.wdesc._name.m_buffer.Contains("Mana Siphon")
                        || parsed.wdesc._name.m_buffer.Contains("Mnemosyne")
                        || parsed.wdesc._name.m_buffer.Contains("Portal")
                        || parsed.wdesc._name.m_buffer.Contains("Door")
                        || parsed.wdesc._name.m_buffer.Contains("Wall")
                        || parsed.wdesc._name.m_buffer.Contains("Pit")
                        || parsed.wdesc._name.m_buffer.Contains("Book")
                        || parsed.wdesc._name.m_buffer.Contains("The Deep")
                        // || parsed.wdesc._name.m_buffer.Contains("Warner Brother")
                        || parsed.wdesc._name.m_buffer.Contains("Fishing")
                        || parsed.wdesc._name.m_buffer.Contains("Bookshelf")
                        || parsed.wdesc._name.m_buffer.Contains("Cavern")
                        || parsed.wdesc._name.m_buffer.Contains("Sword of Frozen Fury")
                        || parsed.wdesc._name.m_buffer.Contains("Coffin")
                        || parsed.wdesc._name.m_buffer.Contains("Silence")
                        || parsed.wdesc._name.m_buffer == "Black"
                        || parsed.wdesc._name.m_buffer.Contains("Eyes")
                        || parsed.wdesc._name.m_buffer.Contains("Bed")
                        || parsed.wdesc._name.m_buffer.Contains("Hole")
                        || parsed.wdesc._name.m_buffer.Contains("Tribunal")
                        || parsed.wdesc._name.m_buffer.Contains("Sunlight")
                        || parsed.wdesc._name.m_buffer.Contains("Wind")
                        || parsed.wdesc._name.m_buffer == "E"
                        || parsed.wdesc._name.m_buffer == "Flame"
                        || parsed.wdesc._name.m_buffer == "Death"
                        || parsed.wdesc._name.m_buffer == "Darkness"
                        || parsed.wdesc._name.m_buffer == "Time"
                        || parsed.wdesc._name.m_buffer == "Ring"
                        || parsed.wdesc._name.m_buffer == "Hope"
                        || parsed.wdesc._name.m_buffer == "Mushroom"
                        || parsed.wdesc._name.m_buffer == "Stars"
                        || parsed.wdesc._name.m_buffer == "Man"
                        || parsed.wdesc._name.m_buffer == "Nothing"
                        || parsed.wdesc._name.m_buffer.Contains("Lever")
                        || parsed.wdesc._name.m_buffer.Contains("Gateway")
                        || parsed.wdesc._name.m_buffer.Contains("Gate Stone")
                        || parsed.wdesc._name.m_buffer.Contains("Target")
                        || parsed.wdesc._name.m_buffer.Contains("Backpack")
                        || parsed.wdesc._name.m_buffer.Contains("Odd Looking Vine")
                        || parsed.wdesc._name.m_buffer.Contains("Pumpkin Vine")
                        || parsed.wdesc._name.m_buffer.Contains("Font")
                        || parsed.wdesc._name.m_buffer.Contains("Lair")
                        || parsed.wdesc._name.m_buffer.Contains("Essence")
                        || parsed.wdesc._name.m_buffer.Contains("Smelting")
                        || parsed.wdesc._name.m_buffer.Contains("Documents")
                        || parsed.wdesc._name.m_buffer.Contains("Harmonic Transference Field")
                        || parsed.wdesc._name.m_buffer.Contains("Deeper into")
                        || parsed.wdesc._name.m_buffer.Contains("Up to the")
                        || parsed.wdesc._name.m_buffer.Contains("Pool")
                        || parsed.wdesc._name.m_buffer == "Hollow"
                        )
                    {
                        fileToPutItIn = "CreaturesOtherNPCs";
                        weenieType = WeenieType.Creature_WeenieType;
                        margin = 20f;
                        addIt = true;
                    }
                    else if (parsed.wdesc._name.m_buffer.Contains("Exploration Marker"))
                    {
                        fileToPutItIn = "CreaturesExplorationMarkers";
                        weenieType = WeenieType.Creature_WeenieType;
                        margin = 20f;
                        addIt = true;
                    }
                    else if (parsed.wdesc._name.m_buffer.Contains("Mysterious Hatch"))
                    {
                        fileToPutItIn = "CreaturesHatches";
                        weenieType = WeenieType.Creature_WeenieType;
                        margin = 20f;
                        addIt = true;
                    }
                    else if (parsed.wdesc._name.m_buffer.Contains("Cow") && !parsed.wdesc._name.m_buffer.Contains("Auroch") && !parsed.wdesc._name.m_buffer.Contains("Snowman"))
                    {
                        fileToPutItIn = "CreaturesCows";
                        weenieType = WeenieType.Cow_WeenieType;
                        addWeenie = true;
                    }
                    //else if (parsed.wdesc._name.m_buffer.Contains("Auroch"))
                    //{
                    //    weeniefileToPutItIn = "CreaturesAurochs";
                    //    weenieType = WeenieType.Cow_WeenieType;
                    //    addWeenie = true;
                    //}
                    else if (parsed.wdesc._wcid >= 14342 && parsed.wdesc._wcid <= 14347
                        || parsed.wdesc._wcid >= 14404 && parsed.wdesc._wcid <= 14409
                        )
                    {
                        fileToPutItIn = "CreaturesChessPieces";
                        weenieType = WeenieType.GamePiece_WeenieType;
                        addWeenie = true;
                    }
                    //else if (parsed.wdesc._radar_enum == RadarEnum.ShowNever_RadarEnum)
                    //{
                    //    fileToPutItIn = "CreaturesNeverShows";
                    //    weenieType = WeenieType.Creature_WeenieType;
                    //    addIt = true;
                    //}
                    else
                    {
                        fileToPutItIn = "CreaturesUnsorted";
                        weenieType = WeenieType.Creature_WeenieType;
                        addWeenie = true;
                    }
                }
                else if (parsed.wdesc._type == ITEM_TYPE.TYPE_GAMEBOARD)
                {
                    fileToPutItIn = "GameBoards";
                    weenieType = WeenieType.Game_WeenieType;
                    addIt = true;
                }
                else if (parsed.object_id < 0x80000000 && parsed.object_id != 50945534)
                {
                    fileToPutItIn = "LandscapeStatics";
                    weenieType = WeenieType.Generic_WeenieType;
                    addIt = true;
                }
                else if (parsed.wdesc._type == ITEM_TYPE.TYPE_ARMOR)
                {
                    fileToPutItIn = "Armor";
                    weenieType = WeenieType.Clothing_WeenieType;
                    addWeenie = true;
                }
                else if (parsed.wdesc._type == ITEM_TYPE.TYPE_MELEE_WEAPON)
                {
                    fileToPutItIn = "MeleeWeapons";
                    weenieType = WeenieType.MeleeWeapon_WeenieType;
                    addWeenie = true;
                }
                else if (parsed.wdesc._type == ITEM_TYPE.TYPE_CLOTHING)
                {
                    fileToPutItIn = "Clothing";
                    weenieType = WeenieType.Clothing_WeenieType;
                    addWeenie = true;
                }
                else if (parsed.wdesc._type == ITEM_TYPE.TYPE_JEWELRY)
                {
                    fileToPutItIn = "Jewelry";
                    weenieType = WeenieType.Clothing_WeenieType;
                    addWeenie = true;
                }
                else if (parsed.wdesc._type == ITEM_TYPE.TYPE_FOOD)
                {
                    fileToPutItIn = "Food";
                    weenieType = WeenieType.Food_WeenieType;
                    addWeenie = true;
                }
                else if (parsed.wdesc._type == ITEM_TYPE.TYPE_MONEY)
                {
                    fileToPutItIn = "Money";
                    weenieType = WeenieType.Coin_WeenieType;
                    addWeenie = true;
                }
                else if (parsed.wdesc._type == ITEM_TYPE.TYPE_MISSILE_WEAPON)
                {
                    fileToPutItIn = "MissileWeapons";
                    weenieType = WeenieType.MissileLauncher_WeenieType;
                    addWeenie = true;
                }
                else if (parsed.wdesc._type == ITEM_TYPE.TYPE_GEM)
                {
                    fileToPutItIn = "Gems";
                    weenieType = WeenieType.Gem_WeenieType;
                    addWeenie = true;
                }
                else if (parsed.wdesc._type == ITEM_TYPE.TYPE_SPELL_COMPONENTS)
                {
                    fileToPutItIn = "SpellComponents";
                    weenieType = WeenieType.SpellComponent_WeenieType;
                    addWeenie = true;
                }
                else if (parsed.wdesc._type == ITEM_TYPE.TYPE_KEY)
                {
                    fileToPutItIn = "Keys";
                    weenieType = WeenieType.Key_WeenieType;
                    addWeenie = true;
                }
                else if (parsed.wdesc._type == ITEM_TYPE.TYPE_CASTER)
                {
                    fileToPutItIn = "Casters";
                    weenieType = WeenieType.Caster_WeenieType;
                    addWeenie = true;
                }
                else if (parsed.wdesc._type == ITEM_TYPE.TYPE_MANASTONE)
                {
                    fileToPutItIn = "ManaStones";
                    weenieType = WeenieType.ManaStone_WeenieType;
                    addWeenie = true;
                }
                else if (parsed.wdesc._type == ITEM_TYPE.TYPE_PROMISSORY_NOTE)
                {
                    fileToPutItIn = "PromissoryNotes";
                    weenieType = WeenieType.Generic_WeenieType;
                    addWeenie = true;
                }
                else if (parsed.wdesc._type == ITEM_TYPE.TYPE_CRAFT_ALCHEMY_BASE)
                {
                    fileToPutItIn = "CraftAlchemyBase";
                    weenieType = WeenieType.Generic_WeenieType;
                    addWeenie = true;
                }
                else if (parsed.wdesc._type == ITEM_TYPE.TYPE_CRAFT_ALCHEMY_INTERMEDIATE)
                {
                    fileToPutItIn = "CraftAlchemyIntermediate";
                    weenieType = WeenieType.Generic_WeenieType;
                    addWeenie = true;
                }
                else if (parsed.wdesc._type == ITEM_TYPE.TYPE_CRAFT_COOKING_BASE)
                {
                    fileToPutItIn = "CraftCookingBase";
                    weenieType = WeenieType.Generic_WeenieType;
                    addWeenie = true;
                }
                else if (parsed.wdesc._type == ITEM_TYPE.TYPE_CRAFT_FLETCHING_BASE)
                {
                    fileToPutItIn = "CraftFletchingBase";
                    weenieType = WeenieType.Generic_WeenieType;
                    addWeenie = true;
                }
                else if (parsed.wdesc._type == ITEM_TYPE.TYPE_CRAFT_FLETCHING_INTERMEDIATE)
                {
                    fileToPutItIn = "CraftFletchingIntermediate";
                    weenieType = WeenieType.Generic_WeenieType;
                    addWeenie = true;
                }
                else if (parsed.wdesc._type == ITEM_TYPE.TYPE_TINKERING_TOOL)
                {
                    fileToPutItIn = "TinkeringTools";
                    weenieType = WeenieType.CraftTool_WeenieType;
                    addWeenie = true;
                }
                else if (parsed.wdesc._type == ITEM_TYPE.TYPE_TINKERING_MATERIAL)
                {
                    fileToPutItIn = "TinkeringMaterials";
                    weenieType = WeenieType.Generic_WeenieType;
                    addWeenie = true;
                }
                //else if (parsed.wdesc._type == ITEM_TYPE.TYPE_USELESS)
                //{
                //    weeniefileToPutItIn = "UselessItems";
                //    addWeenie = true;
                //}
                else if (((uint)parsed.wdesc._type & (uint)ITEM_TYPE.TYPE_ITEM) > 0
                    )
                {
                    fileToPutItIn = "ItemsUnsorted";
                    weenieType = WeenieType.Generic_WeenieType;
                    addWeenie = true;
                }
                else if (parsed.wdesc._name.m_buffer.Contains("Light")
                    || parsed.wdesc._name.m_buffer.Contains("Lantern")
                    || parsed.wdesc._name.m_buffer.Contains("Candelabra")
                    || parsed.wdesc._name.m_buffer.Contains("Stove")
                    || parsed.wdesc._name.m_buffer.Contains("Flame")
                    || parsed.wdesc._name.m_buffer.Contains("Lamp")
                    || parsed.wdesc._name.m_buffer.Contains("Chandelier")
                    || parsed.wdesc._name.m_buffer.Contains("Torch")
                    || parsed.wdesc._name.m_buffer.Contains("Hearth")
                    )
                {
                    fileToPutItIn = "LightSourceObjects";
                    weenieType = WeenieType.LightSource_WeenieType;
                    addWeenie = true;
                }
                else
                {
                    fileToPutItIn = "OtherObjects";
                    weenieType = WeenieType.Generic_WeenieType;
                    addWeenie = true;
                }

                // }

                if (!processedWeeniePositions.ContainsKey(parsed.wdesc._wcid))
                    processedWeeniePositions.Add(parsed.wdesc._wcid, new List<Position>());

                if (addEverything)
                    addIt = true;

                if (addIt)
                {
                    if (parsed.physicsdesc.children.Count > 0)
                    {
                        if (!wieldedObjectsParentMap.ContainsKey(parsed.object_id))
                        {
                            wieldedObjectsParentMap.Add(parsed.object_id, new List<uint>());
                        }

                        foreach (var child in parsed.physicsdesc.children)
                        {
                            wieldedObjectsParentMap[parsed.object_id].Add(child.id);
                        }
                    }
                }

                if (parsed.physicsdesc.parent_id > 0)
                {
                    if (!inventoryParents.ContainsKey(parsed.physicsdesc.parent_id))
                    {
                        inventoryParents.Add(parsed.physicsdesc.parent_id, new List<uint>());
                    }

                    if (!inventoryParents[parsed.physicsdesc.parent_id].Contains(parsed.object_id))
                    {
                        uint weenieDedupe = 0;
                        bool weenieFound = false;
                        foreach (var child in inventoryParents[parsed.physicsdesc.parent_id])
                        {
                            appraisalObjectToWeenieId.TryGetValue(child, out weenieDedupe);
                         
                            if ((weenieDedupe > 0) && weenieDedupe == parsed.wdesc._wcid)
                                weenieFound = true;
                        }

                        if (!weenieFound)
                        {
                            inventoryParents[parsed.physicsdesc.parent_id].Add(parsed.object_id);

                            if (!wieldedObjects.ContainsKey(parsed.physicsdesc.parent_id))
                                wieldedObjects.Add(parsed.physicsdesc.parent_id, new List<CreateObject>());

                            wieldedObjects[parsed.physicsdesc.parent_id].Add(parsed);
                        }

                        if (objectIds.Contains(parsed.physicsdesc.parent_id))
                        {
                            if (wieldedObjectsParentMap.ContainsKey(parsed.physicsdesc.parent_id))
                            {
                                foreach (var child in wieldedObjectsParentMap[parsed.physicsdesc.parent_id])
                                {
                                    if (child == parsed.object_id)
                                    {
                                        //fileToPutItIn = weeniefileToPutItIn;
                                        //addIt = true;

                                        uint weenieId = 0;
                                        appraisalObjectToWeenieId.TryGetValue(parsed.physicsdesc.parent_id, out weenieId);

                                        if (weenieId > 0)
                                        {
                                            if (!parentWieldsWeenies.ContainsKey(weenieId))
                                                parentWieldsWeenies.Add(weenieId, new List<uint>());

                                            if (!parentWieldsWeenies[weenieId].Contains(parsed.wdesc._wcid))
                                                parentWieldsWeenies[weenieId].Add(parsed.wdesc._wcid);
                                        }

                                        //if (!parentWieldsWeenies.ContainsKey(parsed.physicsdesc.parent_id))
                                        //    parentWieldsWeenies.Add(parsed.physicsdesc.parent_id, new List<uint>());

                                        //parentWieldsWeenies[parsed.physicsdesc.parent_id].Add(parsed.wdesc._wcid);

                                        break;
                                    }
                                }
                            }
                        }
                    }               
                }

                if (!weeniesTypeTemplate.ContainsKey((uint)weenieType))
                {
                    bool skip = false;
                    
                    switch (weenieType)
                    {
                        //case WeenieType.Scroll_WeenieType:

                        //    break;
                        default:
                            skip = false;
                            break;
                    }

                    if (!skip)
                        weeniesTypeTemplate.Add((uint)weenieType, parsed);
                }
                
                // de-dupe based on position and wcid
                if ((addIt && !PositionRecorded(parsed, processedWeeniePositions[parsed.wdesc._wcid], parsed.physicsdesc.pos, margin)) || (addIt && !dedupeWeenies))
                // if (addIt) //&& !PositionRecorded(parsed, processedWeeniePositions[parsed.wdesc._wcid], parsed.physicsdesc.pos, margin))
                {
                    if (!weenieIds.Contains(parsed.wdesc._wcid))
                    {
                        if (!weenies.ContainsKey(fileToPutItIn))
                            weenies.Add(fileToPutItIn, new List<CM_Physics.CreateObject>());

                        weenies[fileToPutItIn].Add(parsed);
                        weenieIds.Add(parsed.wdesc._wcid);

                        if (!weenieObjectsCatagoryMap.ContainsKey(parsed.wdesc._wcid))
                            weenieObjectsCatagoryMap.Add(parsed.wdesc._wcid, fileToPutItIn);

                        if (!appraisalObjectsCatagoryMap.ContainsKey(parsed.object_id))
                            appraisalObjectsCatagoryMap.Add(parsed.object_id, fileToPutItIn);
                        if (!appraisalObjectToWeenieId.ContainsKey(parsed.object_id))
                            appraisalObjectToWeenieId.Add(parsed.object_id, parsed.wdesc._wcid);

                        if (!weeniesWeenieType.ContainsKey(parsed.wdesc._wcid))
                            weeniesWeenieType.Add(parsed.wdesc._wcid, (uint)weenieType);
                    }

                    if (!saveAsLandblockInstances)
                    {
                        if (!staticObjects.ContainsKey(fileToPutItIn))
                            staticObjects.Add(fileToPutItIn, new List<CM_Physics.CreateObject>());

                        staticObjects[fileToPutItIn].Add(parsed);                        
                    }
                    else
                    {
                        uint landblock = (parsed.physicsdesc.pos.objcell_id >> 16);
                        if (!landblockInstances.ContainsKey(landblock))
                            landblockInstances.Add(landblock, new Dictionary<string, List<CreateObject>>());

                        if (!landblockInstances[landblock].ContainsKey(fileToPutItIn))
                            landblockInstances[landblock].Add(fileToPutItIn, new List<CM_Physics.CreateObject>());

                        landblockInstances[landblock][fileToPutItIn].Add(parsed);
                    }
                    objectIds.Add(parsed.object_id);

                    if (!appraisalObjectToWeenieId.ContainsKey(parsed.object_id))
                        appraisalObjectToWeenieId.Add(parsed.object_id, parsed.wdesc._wcid);

                    if (!appraisalObjectsCatagoryMap.ContainsKey(parsed.object_id))
                        appraisalObjectsCatagoryMap.Add(parsed.object_id, fileToPutItIn);

                    if (!staticObjectsWeenieType.ContainsKey(parsed.object_id))
                        staticObjectsWeenieType.Add(parsed.object_id, (uint)weenieType);

                    processedWeeniePositions[parsed.wdesc._wcid].Add(parsed.physicsdesc.pos);

                    totalHits++;

                    if (inventoryParents.ContainsKey(parsed.object_id))
                    {
                        foreach (var child in wieldedObjects[parsed.object_id])
                        {
                            if (wieldedObjectsParentMap.ContainsKey(parsed.object_id))
                            {
                                if (wieldedObjectsParentMap[parsed.object_id].Contains(child.object_id))
                                {
                                    string newfileToPutItIn = fileToPutItIn;
                                    if (!saveAsLandblockInstances)
                                    {
                                        if (!staticObjects.ContainsKey(newfileToPutItIn))
                                            staticObjects.Add(newfileToPutItIn, new List<CM_Physics.CreateObject>());

                                        staticObjects[newfileToPutItIn].Add(child);
                                    }
                                    else
                                    {
                                        //uint landblock = (child.physicsdesc.pos.objcell_id >> 16);
                                        //if (!landblockInstances.ContainsKey(landblock))
                                        //    landblockInstances.Add(landblock, new Dictionary<string, List<CreateObject>>());

                                        //if (!landblockInstances[landblock].ContainsKey(fileToPutItIn))
                                        //    landblockInstances[landblock].Add(fileToPutItIn, new List<CM_Physics.CreateObject>());

                                        //landblockInstances[landblock][fileToPutItIn].Add(child);

                                        //if (!parentWieldsWeenies.ContainsKey(child.physicsdesc.parent_id))
                                        //    parentWieldsWeenies.Add(child.physicsdesc.parent_id, new List<uint>());

                                        //parentWieldsWeenies[child.physicsdesc.parent_id].Add(child.wdesc._wcid);

                                        uint weenieId = 0;
                                        appraisalObjectToWeenieId.TryGetValue(child.physicsdesc.parent_id, out weenieId);

                                        if (weenieId > 0)
                                        {
                                            if (!parentWieldsWeenies.ContainsKey(weenieId))
                                                parentWieldsWeenies.Add(weenieId, new List<uint>());

                                            if (!parentWieldsWeenies[weenieId].Contains(child.wdesc._wcid))
                                                parentWieldsWeenies[weenieId].Add(child.wdesc._wcid);
                                        }
                                    }
                                    objectIds.Add(child.object_id);

                                    if (!appraisalObjectToWeenieId.ContainsKey(child.object_id))
                                        appraisalObjectToWeenieId.Add(child.object_id, child.wdesc._wcid);

                                    if (!appraisalObjectsCatagoryMap.ContainsKey(child.object_id))
                                        appraisalObjectsCatagoryMap.Add(child.object_id, newfileToPutItIn);

                                    if (!staticObjectsWeenieType.ContainsKey(child.object_id))
                                        staticObjectsWeenieType.Add(child.object_id, (uint)weenieType);

                                    processedWeeniePositions[child.wdesc._wcid].Add(child.physicsdesc.pos);

                                    totalHits++;
                                }
                            }
                        }
                    }
                }
                else if (addWeenie)
                {
                    if (!weenieIds.Contains(parsed.wdesc._wcid))
                    {
                        if (!weenies.ContainsKey(fileToPutItIn))
                            weenies.Add(fileToPutItIn, new List<CM_Physics.CreateObject>());

                        weenies[fileToPutItIn].Add(parsed);
                        weenieIds.Add(parsed.wdesc._wcid);

                        if (!weenieObjectsCatagoryMap.ContainsKey(parsed.wdesc._wcid))
                            weenieObjectsCatagoryMap.Add(parsed.wdesc._wcid, fileToPutItIn);

                        if (!appraisalObjectsCatagoryMap.ContainsKey(parsed.object_id))
                            appraisalObjectsCatagoryMap.Add(parsed.object_id, fileToPutItIn);
                        if (!appraisalObjectToWeenieId.ContainsKey(parsed.object_id))
                            appraisalObjectToWeenieId.Add(parsed.object_id, parsed.wdesc._wcid);

                        if (!weeniesWeenieType.ContainsKey(parsed.wdesc._wcid))
                            weeniesWeenieType.Add(parsed.wdesc._wcid, (uint)weenieType);

                        totalHits++;
                    }
                }
            }
            catch (Exception ex)
            {
                // MessageBox.Show(ex.ToString());
                totalExceptions++;
            }
        }


        private void WriteGeneratorObjectData(Dictionary<string, List<CM_Physics.CreateObject>> staticObjects, List<uint> objectIds, string outputFolder)
        {
            string staticFolder = Path.Combine(outputFolder, "6-generators");

            string sqlCommand = "INSERT";
            //string sqlCommand = "REPLACE";

            if (!Directory.Exists(staticFolder))
                Directory.CreateDirectory(staticFolder);

            //Dictionary<ITEM_TYPE, int> fileCount = new Dictionary<ITEM_TYPE, int>();
            Dictionary<string, int> fileCount = new Dictionary<string, int>();

            foreach (string key in staticObjects.Keys)
            {
                //string filename = Path.Combine(staticFolder, $"{key}.sql");

                //if (File.Exists(filename))
                //    File.Delete(filename);

                //if (!fileCount.ContainsKey(key))
                //    fileCount.Add(key, 0);

                ////string fullFile = Path.Combine(staticFolder, $"{filename}");
                //string fullFile = Path.Combine(staticFolder, $"{key}_{fileCount[key]}.sql");

                //if (File.Exists(fullFile))
                //{
                //    FileInfo fi = new FileInfo(fullFile);

                //    // go to the next file if it's bigger than a MB
                //    if (fi.Length > ((1048576) * 40))
                //    {
                //        //fileCount[parsed.wdesc._type]++;
                //        fileCount[key]++;
                //        // fullFile = Path.Combine(staticFolder, $"{parsed.wdesc._type}_{fileCount[parsed.wdesc._type]}.sql");
                //        fullFile = Path.Combine(staticFolder, $"{key}_{fileCount[key]}.sql");

                //        if (File.Exists(fullFile))
                //            File.Delete(fullFile);
                //    }
                //}

                foreach (var parsed in staticObjects[key])
                {
                    try
                    {
                        //if (!fileCount.ContainsKey(key))
                        //    fileCount.Add(key, 0);

                        ////string fullFile = Path.Combine(staticFolder, $"{filename}");
                        //string fullFile = Path.Combine(staticFolder, $"{key}_{fileCount[key]}.sql");

                        //if (File.Exists(fullFile))
                        //{
                        //    FileInfo fi = new FileInfo(fullFile);

                        //    // go to the next file if it's bigger than a MB
                        //    if (fi.Length > ((1048576) * 40))
                        //    {
                        //        //fileCount[parsed.wdesc._type]++;
                        //        fileCount[key]++;
                        //        // fullFile = Path.Combine(staticFolder, $"{parsed.wdesc._type}_{fileCount[parsed.wdesc._type]}.sql");
                        //        fullFile = Path.Combine(staticFolder, $"{key}_{fileCount[key]}.sql");

                        //        if (File.Exists(fullFile))
                        //            File.Delete(fullFile);
                        //    }
                        //}

                        if (!fileCount.ContainsKey(key))
                            fileCount.Add(key, 0);

                        //string fullFile = Path.Combine(staticFolder, $"{filename}");
                        string fullFile = Path.Combine(staticFolder, $"{key}_{fileCount[key]}.sql");

                        if (File.Exists(fullFile))
                        {
                            FileInfo fi = new FileInfo(fullFile);

                            // go to the next file if it's bigger than a MB
                            if (fi.Length > ((1048576) * 40))
                            {
                                //fileCount[parsed.wdesc._type]++;
                                fileCount[key]++;
                                // fullFile = Path.Combine(staticFolder, $"{parsed.wdesc._type}_{fileCount[parsed.wdesc._type]}.sql");
                                fullFile = Path.Combine(staticFolder, $"{key}_{fileCount[key]}.sql");

                                if (File.Exists(fullFile))
                                    File.Delete(fullFile);
                            }
                        }

                        using (FileStream fs = new FileStream(fullFile, FileMode.Append))
                        {
                            using (StreamWriter writer = new StreamWriter(fs))
                            {
                                string weenieName = "";
                                if (Enum.IsDefined(typeof(WeenieClasses), (ushort)parsed.wdesc._wcid))
                                {
                                    weenieName = Enum.GetName(typeof(WeenieClasses), parsed.wdesc._wcid).Substring(2);
                                    //weenieName = weenieName.Substring(0, weenieName.Length - 6).Replace("_", "").ToLower();
                                    weenieName = weenieName.Substring(0, weenieName.Length - 6).Replace("_", "-").ToLower();
                                }
                                else
                                    weenieName = "ace" + parsed.wdesc._wcid.ToString() + "-" + parsed.wdesc._name.m_buffer.Replace("'", "").Replace(" ", "").Replace(".", "").Replace("(", "").Replace(")", "").Replace("+", "").Replace(":", "").Replace("_", "").Replace("-", "").ToLower();

                                string genline = "";

                                genline += "USE `ace_world`;" + Environment.NewLine;
                                genline += Environment.NewLine;
                                genline += "/* Generator Setup Variables */" + Environment.NewLine;
                                genline += $"SET @weenieClassId = {parsed.wdesc._wcid};" + Environment.NewLine;
                                genline += $"SET @weenieClassDescription = '{weenieName}';" + Environment.NewLine;
                                genline += "SET @generatorClassId = 5485;" + Environment.NewLine;
                                genline += Environment.NewLine;
                                genline += $"SET @name = '{parsed.wdesc._name.m_buffer.Replace("'", "''")} Generator';" + Environment.NewLine;
                                genline += Environment.NewLine;
                                genline += "SET @ActivationCreateClass = @weenieClassId;" + Environment.NewLine;
                                genline += "SET @MaxGeneratedObjects = 1;" + Environment.NewLine;
                                genline += "SET @GeneratorType = 2;" + Environment.NewLine;
                                genline += "SET @GeneratorTimeType = 0;" + Environment.NewLine;
                                genline += "SET @GeneratorProbability = 100;" + Environment.NewLine;
                                genline += Environment.NewLine;
                                genline += "SET @RegenerationInterval = 120; /* RegenerationInterval in seconds */" + Environment.NewLine;
                                genline += Environment.NewLine;
                                genline += $"SET @landblockRaw = {parsed.physicsdesc.pos.objcell_id};" + Environment.NewLine;
                                genline += $"SET @posX = {parsed.physicsdesc.pos.frame.m_fOrigin.x};" + Environment.NewLine;
                                genline += $"SET @posY = {parsed.physicsdesc.pos.frame.m_fOrigin.y};" + Environment.NewLine;
                                genline += $"SET @posZ = {parsed.physicsdesc.pos.frame.m_fOrigin.z};" + Environment.NewLine;
                                genline += $"SET @qW = {parsed.physicsdesc.pos.frame.qw};" + Environment.NewLine;
                                genline += $"SET @qX = {parsed.physicsdesc.pos.frame.qx};" + Environment.NewLine;
                                genline += $"SET @qY = {parsed.physicsdesc.pos.frame.qy};" + Environment.NewLine;
                                genline += $"SET @qZ = {parsed.physicsdesc.pos.frame.qz};" + Environment.NewLine;
                                genline += Environment.NewLine;
                                genline += "/* Add generator instances */" + Environment.NewLine;
                                genline += "INSERT INTO ace_object" + Environment.NewLine;
                                genline += "    (aceObjectDescriptionFlags," + Environment.NewLine;
                                genline += "    weenieClassId)" + Environment.NewLine;
                                genline += "SELECT" + Environment.NewLine;
                                genline += "    aceObjectDescriptionFlags," + Environment.NewLine;
                                genline += "    weenieClassId" + Environment.NewLine;
                                genline += "FROM ace_object" + Environment.NewLine;
                                genline += "WHERE aceObjectId = @generatorClassId;" + Environment.NewLine;
                                genline += Environment.NewLine;
                                genline += "SET SQL_SAFE_UPDATES = 0;" + Environment.NewLine;
                                genline += "CREATE TEMPORARY TABLE tmp SELECT* from ace_object_properties_did WHERE aceObjectId = @generatorClassId;" + Environment.NewLine;
                                genline += "UPDATE tmp SET aceObjectId = last_insert_id();" + Environment.NewLine;
                                genline += "INSERT INTO ace_object_properties_did SELECT tmp.* FROM tmp;" + Environment.NewLine;
                                genline += "DROP TEMPORARY TABLE tmp;" + Environment.NewLine;
                                genline += "SET SQL_SAFE_UPDATES = 1;" + Environment.NewLine;
                                genline += Environment.NewLine;
                                genline += "SET SQL_SAFE_UPDATES = 0;" + Environment.NewLine;
                                genline += "CREATE TEMPORARY TABLE tmp SELECT* from ace_object_properties_int WHERE aceObjectId = @generatorClassId;" + Environment.NewLine;
                                genline += "UPDATE tmp SET aceObjectId = last_insert_id();" + Environment.NewLine;
                                genline += "UPDATE tmp SET propertyValue = @MaxGeneratedObjects WHERE intPropertyId = 81;" + Environment.NewLine;
                                genline += "UPDATE tmp SET propertyValue = @GeneratorType WHERE intPropertyId = 100;" + Environment.NewLine;
                                genline += "UPDATE tmp SET propertyValue = @ActivationCreateClass WHERE intPropertyId = 104;" + Environment.NewLine;
                                genline += "UPDATE tmp SET propertyValue = @GeneratorTimeType WHERE intPropertyId = 142;" + Environment.NewLine;
                                genline += "UPDATE tmp SET propertyValue = @GeneratorProbability WHERE intPropertyId = 9006;" + Environment.NewLine;
                                genline += "INSERT INTO ace_object_properties_int SELECT tmp.* FROM tmp;" + Environment.NewLine;
                                genline += "DROP TEMPORARY TABLE tmp;" + Environment.NewLine;
                                genline += "SET SQL_SAFE_UPDATES = 1;" + Environment.NewLine;
                                genline += Environment.NewLine;
                                genline += "SET SQL_SAFE_UPDATES = 0;" + Environment.NewLine;
                                genline += "CREATE TEMPORARY TABLE tmp SELECT* from ace_object_properties_double WHERE aceObjectId = @generatorClassId;" + Environment.NewLine;
                                genline += "UPDATE tmp SET aceObjectId = last_insert_id();" + Environment.NewLine;
                                genline += "UPDATE tmp SET propertyValue = @RegenerationInterval WHERE dblPropertyId = 41;" + Environment.NewLine;
                                genline += "INSERT INTO ace_object_properties_double SELECT tmp.* FROM tmp;" + Environment.NewLine;
                                genline += "DROP TEMPORARY TABLE tmp;" + Environment.NewLine;
                                genline += "SET SQL_SAFE_UPDATES = 1;" + Environment.NewLine;
                                genline += Environment.NewLine;
                                genline += "SET SQL_SAFE_UPDATES = 0;" + Environment.NewLine;
                                genline += "CREATE TEMPORARY TABLE tmp SELECT* from ace_object_properties_bool WHERE aceObjectId = @generatorClassId;" + Environment.NewLine;
                                genline += "UPDATE tmp SET aceObjectId = last_insert_id();" + Environment.NewLine;
                                genline += "INSERT INTO ace_object_properties_bool SELECT tmp.* FROM tmp;" + Environment.NewLine;
                                genline += "DROP TEMPORARY TABLE tmp;" + Environment.NewLine;
                                genline += "SET SQL_SAFE_UPDATES = 1;" + Environment.NewLine;
                                genline += Environment.NewLine;
                                genline += "SET SQL_SAFE_UPDATES = 0;" + Environment.NewLine;
                                genline += "CREATE TEMPORARY TABLE tmp SELECT* from ace_object_properties_string WHERE aceObjectId = @generatorClassId;" + Environment.NewLine;
                                genline += "UPDATE tmp SET aceObjectId = last_insert_id();" + Environment.NewLine;
                                genline += "UPDATE tmp SET propertyValue = @name WHERE strPropertyId = 1;" + Environment.NewLine;
                                genline += "INSERT INTO ace_object_properties_string SELECT tmp.* FROM tmp;" + Environment.NewLine;
                                genline += "DROP TEMPORARY TABLE tmp;" + Environment.NewLine;
                                genline += "SET SQL_SAFE_UPDATES = 1;" + Environment.NewLine;
                                genline += Environment.NewLine;
                                genline += "/*" + Environment.NewLine;
                                genline += "SET SQL_SAFE_UPDATES = 0;" + Environment.NewLine;
                                genline += "CREATE TEMPORARY TABLE tmp SELECT * from ace_object_generator_link WHERE aceObjectId = @generatorClassId;" + Environment.NewLine;
                                genline += "UPDATE tmp SET aceObjectId = last_insert_id();" + Environment.NewLine;
                                genline += "INSERT INTO ace_object_generator_link SELECT tmp.* FROM tmp;" + Environment.NewLine;
                                genline += "DROP TEMPORARY TABLE tmp;" + Environment.NewLine;
                                genline += "SET SQL_SAFE_UPDATES = 1;" + Environment.NewLine;
                                genline += "*/" + Environment.NewLine;
                                genline += Environment.NewLine;
                                genline += "INSERT INTO ace_position" + Environment.NewLine;
                                genline += "    (aceObjectId," + Environment.NewLine;
                                genline += "    positionType," + Environment.NewLine;
                                genline += "    landblockRaw," + Environment.NewLine;
                                genline += "    posX," + Environment.NewLine;
                                genline += "    posY," + Environment.NewLine;
                                genline += "    posZ," + Environment.NewLine;
                                genline += "    qW," + Environment.NewLine;
                                genline += "    qX," + Environment.NewLine;
                                genline += "    qY," + Environment.NewLine;
                                genline += "    qZ)" + Environment.NewLine;
                                genline += "VALUES" + Environment.NewLine;
                                genline += "    (last_insert_id(), 1, @landblockRaw, @posX, @posY, @posZ, @qW, @qX, @qY, @qZ);" + Environment.NewLine;

                                writer.WriteLine(genline);                                
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("Unable to export object " + parsed.object_id + ". Exception:" + Environment.NewLine + ex.ToString());
                    }
                }
            }
        }

        private void WriteLandblockTable(Dictionary<uint, Dictionary<string, List<CM_Physics.CreateObject>>> landblockInstances, List<uint> objectIds, string outputFolder, Dictionary<uint, uint> staticObjectsWeenieType)
        {
            bool useHex = true;
            bool useCategories = false;
            string staticFolder = Path.Combine(outputFolder, "6-landblocks");

            string sqlCommand = "INSERT";
            //string sqlCommand = "REPLACE";

            if (!Directory.Exists(staticFolder))
                Directory.CreateDirectory(staticFolder);

            //Dictionary<string, int> fileCount = new Dictionary<string, int>();

            foreach (uint landblock in landblockInstances.Keys)
            {
                foreach (string key in landblockInstances[landblock].Keys)
                {
                    //foreach (var parsed in landblockInstances[landblock][key])
                    //{
                    try
                    {
                        string landblockString = landblock.ToString();
                        if (useHex)
                            landblockString = landblock.ToString("X4");
                        //if (!fileCount.ContainsKey(key))
                        //    fileCount.Add(key, 0);

                        //string fullFile = Path.Combine(staticFolder, $"{key}_{fileCount[key]}.sql");
                        string fullFile = "";

                        if (useCategories)
                            fullFile = Path.Combine(staticFolder, $"{landblockString}_{key}.sql");
                        else
                            fullFile = Path.Combine(staticFolder, $"{landblockString}.sql");

                        if (File.Exists(fullFile))
                        {
                            FileInfo fi = new FileInfo(fullFile);

                            // go to the next file if it's bigger than a MB
                            if (fi.Length > ((1048576) * 40))
                            {
                                //fileCount[key]++;
                                //fullFile = Path.Combine(staticFolder, $"{key}_{fileCount[key]}.sql");
                                if (useCategories)
                                    fullFile = Path.Combine(staticFolder, $"{landblockString}_{key}.sql");
                                else
                                    fullFile = Path.Combine(staticFolder, $"{landblockString}.sql");

                                if (useCategories)
                                {
                                    if (File.Exists(fullFile))
                                        File.Delete(fullFile);
                                }
                            }
                        }

                        using (FileStream fs = new FileStream(fullFile, FileMode.Append))
                        {
                            using (StreamWriter writer = new StreamWriter(fs))
                            {


                                string instanceLine = ""; //intsLine = "", bigintsLine = "", floatsLine = "", boolsLine = "", strsLine = "", didsLine = "", iidsLine = "";

                                //intsLine += $"     , ({vendor.shopVendorID}, {(uint)STypeInt.MERCHANDISE_ITEM_TYPES_INT}, {(uint)vendor.shopVendorProfile.item_types})" + Environment.NewLine;
                                //intsLine += $"     , ({vendor.shopVendorID}, {(uint)STypeInt.MERCHANDISE_MIN_VALUE_INT}, {(uint)vendor.shopVendorProfile.min_value})" + Environment.NewLine;
                                //intsLine += $"     , ({vendor.shopVendorID}, {(uint)STypeInt.MERCHANDISE_MAX_VALUE_INT}, {(uint)vendor.shopVendorProfile.max_value})" + Environment.NewLine;

                                //if (vendor.shopVendorProfile.magic == 1)
                                //    boolsLine += $"     , ({vendor.shopVendorID}, {(uint)STypeBool.DEAL_MAGICAL_ITEMS_BOOL}, {true})" + Environment.NewLine;

                                //floatsLine += $"     , ({vendor.shopVendorID}, {(uint)STypeFloat.BUY_PRICE_FLOAT}, {(float)vendor.shopVendorProfile.buy_price})" + Environment.NewLine;
                                //floatsLine += $"     , ({vendor.shopVendorID}, {(uint)STypeFloat.SELL_PRICE_FLOAT}, {(float)vendor.shopVendorProfile.sell_price})" + Environment.NewLine;

                                //line = $"{sqlCommand} INTO `ace_position` (`aceObjectId`, `positionType`, `landblockRaw`, `posX`, `posY`, `posZ`, `qW`, `qX`, `qY`, `qZ`)" + Environment.NewLine +
                                //    //line = $"{sqlCommand} INTO `ace_position` (`aceObjectId`, `positionType`, `landblockRaw`, `landblock`, `cell`, `posX`, `posY`, `posZ`, `qW`, `qX`, `qY`, `qZ`)" + Environment.NewLine +
                                //    $"VALUES ({parsed.object_id}, {(uint)STypePosition.LOCATION_POSITION}, {parsed.physicsdesc.pos.objcell_id}, " +
                                //    //$"{parsed.physicsdesc.pos.objcell_id >> 16}, {parsed.physicsdesc.pos.objcell_id & 0xFFFF}, " +
                                //    $"{parsed.physicsdesc.pos.frame.m_fOrigin.x}, {parsed.physicsdesc.pos.frame.m_fOrigin.y}, {parsed.physicsdesc.pos.frame.m_fOrigin.z}, " +
                                //    $"{parsed.physicsdesc.pos.frame.qw}, {parsed.physicsdesc.pos.frame.qx}, {parsed.physicsdesc.pos.frame.qy}, {parsed.physicsdesc.pos.frame.qz});" +
                                //    Environment.NewLine;

                                foreach (var parsed in landblockInstances[landblock][key])
                                {
                                    instanceLine += $"     , ({parsed.wdesc._wcid}, {parsed.object_id}, " +
                                            $"{parsed.physicsdesc.pos.objcell_id}, " +
                                            $"{parsed.physicsdesc.pos.frame.m_fOrigin.x}, {parsed.physicsdesc.pos.frame.m_fOrigin.y}, {parsed.physicsdesc.pos.frame.m_fOrigin.z}, " +
                                            $"{parsed.physicsdesc.pos.frame.qw}, {parsed.physicsdesc.pos.frame.qx}, {parsed.physicsdesc.pos.frame.qy}, {parsed.physicsdesc.pos.frame.qz}" +
                                        ")" + Environment.NewLine;
                                }

                                if (instanceLine != "")
                                {
                                    if (useCategories)
                                    {
                                        instanceLine = $"{sqlCommand} INTO `ace_landblock` (`weenieClassId`, `preassignedGuid`, `landblockRaw`, `posX`, `posY`, `posZ`, `qW`, `qX`, `qY`, `qZ`)" + Environment.NewLine 
                                            + "VALUES " + instanceLine.TrimStart("     ,".ToCharArray());
                                    }
                                    else
                                    {
                                        instanceLine = $"/* {key} */" + Environment.NewLine
                                            + $"{sqlCommand} INTO `ace_landblock` (`weenieClassId`, `preassignedGuid`, `landblockRaw`, `posX`, `posY`, `posZ`, `qW`, `qX`, `qY`, `qZ`)" + Environment.NewLine
                                            + "VALUES " + instanceLine.TrimStart("     ,".ToCharArray());
                                    }
                                    instanceLine = instanceLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(instanceLine);
                                }

                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("Unable to export landblock " + landblock + ". Exception:" + Environment.NewLine + ex.ToString());
                    }
                    // }
                }
            }
        }

        private void WriteLandblockData(Dictionary<uint, Dictionary<string, List<CM_Physics.CreateObject>>> landblockInstances, 
            List<uint> objectIds, string outputFolder, Dictionary<uint, uint> staticObjectsWeenieType,
            bool generateNewGuids)
        {
            bool useHex = true;
            bool useCategories = false;
            string staticFolder = Path.Combine(outputFolder, "6-landblocks");

            string sqlCommand = "INSERT";
            //string sqlCommand = "REPLACE";

            if (!Directory.Exists(staticFolder))
                Directory.CreateDirectory(staticFolder);

            //Dictionary<string, int> fileCount = new Dictionary<string, int>();

            foreach (uint landblock in landblockInstances.Keys)
            {
                foreach (string key in landblockInstances[landblock].Keys)
                {
                    foreach (var parsed in landblockInstances[landblock][key])
                    {
                        try
                        {
                            string landblockString = landblock.ToString();
                            if (useHex)
                                landblockString = landblock.ToString("X4");
                            //if (!fileCount.ContainsKey(key))
                            //    fileCount.Add(key, 0);

                            //string fullFile = Path.Combine(staticFolder, $"{key}_{fileCount[key]}.sql");
                            string fullFile = "";
                            if (useCategories)
                                fullFile = Path.Combine(staticFolder, $"{landblockString}_{key}.sql");
                            else
                                fullFile = Path.Combine(staticFolder, $"{landblockString}.sql");

                            if (File.Exists(fullFile))
                            {
                                FileInfo fi = new FileInfo(fullFile);

                                // go to the next file if it's bigger than a MB
                                if (fi.Length > ((1048576) * 40))
                                {
                                    //fileCount[key]++;
                                    //fullFile = Path.Combine(staticFolder, $"{key}_{fileCount[key]}.sql");
                                    if (useCategories)
                                        fullFile = Path.Combine(staticFolder, $"{landblockString}_{key}.sql");
                                    else
                                        fullFile = Path.Combine(staticFolder, $"{landblockString}.sql");

                                    if (useCategories)
                                    {
                                        if (File.Exists(fullFile))
                                            File.Delete(fullFile);
                                    }
                                }
                            }

                            using (FileStream fs = new FileStream(fullFile, FileMode.Append))
                            {
                                using (StreamWriter writer = new StreamWriter(fs))
                                {
                                    string intsLine = "", bigintsLine = "", floatsLine = "", boolsLine = "", strsLine = "", didsLine = "", iidsLine = "";

                                    string line = $"/* {key} : {parsed.wdesc._name.m_buffer} */" + Environment.NewLine;

                                    line += $"DELETE FROM ace_landblock WHERE preassignedGuid = {parsed.object_id};" + Environment.NewLine;

                                    line += $"DELETE FROM ace_object WHERE aceObjectId = {parsed.object_id};" + Environment.NewLine + Environment.NewLine;

                                    line += $"{sqlCommand} INTO `ace_object` ("; // +
                                    if (generateNewGuids)
                                    {
                                        line += "`aceObjectDescriptionFlags`, "; // +
                                    }
                                    else
                                    {
                                        line += "`aceObjectId`, `aceObjectDescriptionFlags`, "; // +
                                    }
                                    line += "`weenieClassId`, `weenieHeaderFlags`, " +
                                    "`weenieHeaderFlags2`, " +
                                    "`currentMotionState`, " +
                                    "`physicsDescriptionFlag`" +
                                    ")" + Environment.NewLine + "VALUES ("; // +

                                    if (generateNewGuids)
                                        line += $"{parsed.wdesc._bitfield}, "; //+
                                    else
                                        line += $"{parsed.object_id}, {parsed.wdesc._bitfield}, "; // +

                                    line += $"{parsed.wdesc._wcid}, {parsed.wdesc.header}, "; //+

                                    if ((parsed.wdesc._bitfield & (uint)PublicWeenieDesc.BitfieldIndex.BF_INCLUDES_SECOND_HEADER) != 0)
                                        line += $"{parsed.wdesc.header2}, "; //+
                                    else
                                        line += $"NULL, "; //+

                                    string guid;

                                    if (generateNewGuids)
                                        guid = "last_insert_id()";
                                    else
                                        guid = parsed.object_id.ToString();

                                    didsLine += $"     , ({guid}, {(uint)STypeDID.ICON_DID}, {(uint)parsed.wdesc._iconID})" + Environment.NewLine;
                                    if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_IconOverlay) != 0)
                                        didsLine += $"     , ({guid}, {(uint)STypeDID.ICON_OVERLAY_DID}, {(uint)parsed.wdesc._iconOverlayID})" + Environment.NewLine;
                                    if ((parsed.wdesc.header2 & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader2.PWD2_Packed_IconUnderlay) != 0)
                                        didsLine += $"     , ({guid}, {(uint)STypeDID.ICON_UNDERLAY_DID}, {(uint)parsed.wdesc._iconUnderlayID})" + Environment.NewLine;

                                    if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.CSetup) != 0)
                                        didsLine += $"     , ({guid}, {(uint)STypeDID.SETUP_DID}, {(uint)parsed.physicsdesc.setup_id})" + Environment.NewLine;
                                    if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.STABLE) != 0)
                                        didsLine += $"     , ({guid}, {(uint)STypeDID.SOUND_TABLE_DID}, {(uint)parsed.physicsdesc.stable_id})" + Environment.NewLine;
                                    if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.MTABLE) != 0)
                                        didsLine += $"     , ({guid}, {(uint)STypeDID.MOTION_TABLE_DID}, {(uint)parsed.physicsdesc.mtable_id})" + Environment.NewLine;
                                    if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.MOVEMENT) != 0)
                                        line += $"'{ConvertMovementBufferToString(parsed.physicsdesc.movement_buffer)}', ";
                                    else
                                        line += $"NULL, ";
                                    if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.ANIMFRAME_ID) != 0)
                                        intsLine += $"     , ({guid}, {(uint)STypeInt.PLACEMENT_POSITION_INT}, {(uint)parsed.physicsdesc.animframe_id})" + Environment.NewLine;
                                    if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.PETABLE) != 0)
                                        didsLine += $"     , ({guid}, {(uint)STypeDID.PHYSICS_EFFECT_TABLE_DID}, {(uint)parsed.physicsdesc.phstable_id})" + Environment.NewLine;
                                    line += $"{parsed.physicsdesc.bitfield}";
                                    if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_SpellID) != 0)
                                        didsLine += $"     , ({guid}, {(uint)STypeDID.SPELL_DID}, {(uint)parsed.wdesc._spellID})" + Environment.NewLine;
                                    if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_PScript) != 0)
                                        didsLine += $"     , ({guid}, {(uint)STypeDID.PHYSICS_SCRIPT_DID}, {(uint)parsed.wdesc._pscript})" + Environment.NewLine;
                                    if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.DEFAULT_SCRIPT) != 0)
                                        didsLine += $"     , ({guid}, {(uint)STypeDID.USE_USER_ANIMATION_DID}, {(uint)parsed.physicsdesc.default_script})" + Environment.NewLine;

                                    line += ");" + Environment.NewLine;

                                    writer.WriteLine(line);

                                    strsLine += $"     , ({guid}, {(uint)STypeString.NAME_STRING}, '{parsed.wdesc._name.m_buffer.Replace("'", "''")}')" + Environment.NewLine;

                                    intsLine += $"     , ({guid}, {(uint)STypeInt.ITEM_TYPE_INT}, {(uint)parsed.wdesc._type})" + Environment.NewLine;

                                    if (parsed.objdesc.subpalettes.Count > 0)
                                        //intsLine += $"     , ({parsed.object_id}, {(uint)STypeInt.PALETTE_TEMPLATE_INT}, {parsed.objdesc.paletteID})" + Environment.NewLine;
                                        didsLine += $"     , ({guid}, {(uint)STypeDID.PALETTE_BASE_DID}, {(uint)parsed.objdesc.paletteID})" + Environment.NewLine;

                                    if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_AmmoType) != 0)
                                        intsLine += $"     , ({guid}, {(uint)STypeInt.AMMO_TYPE_INT}, {(uint)parsed.wdesc._ammoType})" + Environment.NewLine;
                                    if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_BlipColor) != 0)
                                        intsLine += $"     , ({guid}, {(uint)STypeInt.RADARBLIP_COLOR_INT}, {parsed.wdesc._blipColor})" + Environment.NewLine;
                                    if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_Burden) != 0)
                                        intsLine += $"     , ({guid}, {(uint)STypeInt.ENCUMB_VAL_INT}, {parsed.wdesc._burden})" + Environment.NewLine;
                                    if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_CombatUse) != 0)
                                        intsLine += $"     , ({guid}, {(uint)STypeInt.COMBAT_USE_INT}, {parsed.wdesc._combatUse})" + Environment.NewLine;
                                    if ((parsed.wdesc.header2 & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader2.PWD2_Packed_CooldownDuration) != 0)
                                        floatsLine += $"     , ({guid}, {(uint)STypeFloat.COOLDOWN_DURATION_FLOAT}, {parsed.wdesc._cooldown_duration})" + Environment.NewLine;
                                    if ((parsed.wdesc.header2 & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader2.PWD2_Packed_CooldownID) != 0)
                                        intsLine += $"     , ({guid}, {(uint)STypeInt.SHARED_COOLDOWN_INT}, {parsed.wdesc._cooldown_id})" + Environment.NewLine;
                                    if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_UIEffects) != 0)
                                        intsLine += $"     , ({guid}, {(uint)STypeInt.UI_EFFECTS_INT}, {parsed.wdesc._effects})" + Environment.NewLine;
                                    if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_ContainersCapacity) != 0)
                                        intsLine += $"     , ({guid}, {(uint)STypeInt.CONTAINERS_CAPACITY_INT}, {parsed.wdesc._containersCapacity})" + Environment.NewLine;
                                    if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_HookType) != 0)
                                        intsLine += $"     , ({guid}, {(uint)STypeInt.HOOK_TYPE_INT}, {(uint)parsed.wdesc._hook_type})" + Environment.NewLine;
                                    if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_HookItemTypes) != 0)
                                        intsLine += $"     , ({guid}, {(uint)STypeInt.HOOK_ITEM_TYPE_INT}, {parsed.wdesc._hook_item_types})" + Environment.NewLine;
                                    if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_ItemsCapacity) != 0)
                                        intsLine += $"     , ({guid}, {(uint)STypeInt.ITEMS_CAPACITY_INT}, {parsed.wdesc._itemsCapacity})" + Environment.NewLine;
                                    if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_Location) != 0)
                                        intsLine += $"     , ({guid}, {(uint)STypeInt.CURRENT_WIELDED_LOCATION_INT}, {parsed.wdesc._location})" + Environment.NewLine;
                                    if ((parsed.wdesc.header & unchecked((uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_MaterialType)) != 0)
                                        intsLine += $"     , ({guid}, {(uint)STypeInt.MATERIAL_TYPE_INT}, {(uint)parsed.wdesc._material_type})" + Environment.NewLine;
                                    if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_MaxStackSize) != 0)
                                        intsLine += $"     , ({guid}, {(uint)STypeInt.MAX_STACK_SIZE_INT}, {parsed.wdesc._maxStackSize})" + Environment.NewLine;
                                    if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_MaxStructure) != 0)
                                        intsLine += $"     , ({guid}, {(uint)STypeInt.MAX_STRUCTURE_INT}, {parsed.wdesc._maxStructure})" + Environment.NewLine;
                                    if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_RadarEnum) != 0)
                                        intsLine += $"     , ({guid}, {(uint)STypeInt.SHOWABLE_ON_RADAR_INT}, {(uint)parsed.wdesc._radar_enum})" + Environment.NewLine;
                                    if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_StackSize) != 0)
                                        intsLine += $"     , ({guid}, {(uint)STypeInt.STACK_SIZE_INT}, {parsed.wdesc._stackSize})" + Environment.NewLine;
                                    if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_Structure) != 0)
                                        intsLine += $"     , ({guid}, {(uint)STypeInt.STRUCTURE_INT}, {parsed.wdesc._structure})" + Environment.NewLine;
                                    if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_TargetType) != 0)
                                        intsLine += $"     , ({guid}, {(uint)STypeInt.TARGET_TYPE_INT}, {(uint)parsed.wdesc._targetType})" + Environment.NewLine;
                                    if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_Useability) != 0)
                                        intsLine += $"     , ({guid}, {(uint)STypeInt.ITEM_USEABLE_INT}, {(uint)parsed.wdesc._useability})" + Environment.NewLine;
                                    if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_UseRadius) != 0)
                                        floatsLine += $"     , ({guid}, {(uint)STypeFloat.USE_RADIUS_FLOAT}, {parsed.wdesc._useRadius})" + Environment.NewLine;
                                    if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_ValidLocations) != 0)
                                        intsLine += $"     , ({guid}, {(uint)STypeInt.LOCATIONS_INT}, {parsed.wdesc._valid_locations})" + Environment.NewLine;
                                    if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_Value) != 0)
                                        intsLine += $"     , ({guid}, {(uint)STypeInt.VALUE_INT}, {parsed.wdesc._value})" + Environment.NewLine;
                                    //if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_ContainerID) != 0)

                                    if (((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_ContainerID) != 0) && objectIds.Contains(parsed.wdesc._containerID))
                                        if (!generateNewGuids)
                                            iidsLine += $"     , ({guid}, {(uint)STypeIID.CONTAINER_IID}, {(uint)parsed.wdesc._containerID})" + Environment.NewLine;

                                    //{
                                    //    line = $"{sqlCommand} INTO `ace_object_properties_int` (`aceObjectId`, `intPropertyId`, `propertyValue`)" + Environment.NewLine;
                                    //    line += $"VALUES ({parsed.object_id}, {(uint)STypeInt.???}, {parsed.wdesc._containerID})" + Environment.NewLine;
                                    //    line = line.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    //    writer.WriteLine(line);
                                    //}
                                    //if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_WielderID) != 0)
                                    //{
                                    //    line = $"{sqlCommand} INTO `ace_object_properties_int` (`aceObjectId`, `intPropertyId`, `propertyValue`)" + Environment.NewLine;
                                    //    line += $"VALUES ({parsed.object_id}, {(uint)STypeInt.???}, {parsed.wdesc._wielderID})" + Environment.NewLine;
                                    //    line = line.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    //    writer.WriteLine(line);
                                    //}
                                    //if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_WielderID) != 0)
                                    //    intsLine += $"     , ({parsed.object_id}, {(uint)STypeIID.WIELDER_IID}, {parsed.wdesc._wielderID})" + Environment.NewLine;

                                    if (((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_WielderID) != 0) && objectIds.Contains(parsed.wdesc._wielderID))
                                        if (!generateNewGuids)
                                            iidsLine += $"     , ({guid}, {(uint)STypeIID.WIELDER_IID}, {(uint)parsed.wdesc._wielderID})" + Environment.NewLine;

                                    //if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_HouseOwner) != 0)
                                    //{
                                    //    line = $"{sqlCommand} INTO `ace_object_properties_int` (`aceObjectId`, `intPropertyId`, `propertyValue`)" + Environment.NewLine;
                                    //    line += $"VALUES ({parsed.object_id}, {(uint)STypeInt.???}, {parsed.wdesc._house_owner_iid})" + Environment.NewLine;
                                    //    line = line.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    //    writer.WriteLine(line);
                                    //}
                                    //if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_HouseRestrictions) != 0)
                                    //{
                                    //    line = $"{sqlCommand} INTO `ace_object_properties_int` (`aceObjectId`, `intPropertyId`, `propertyValue`)" + Environment.NewLine;
                                    //    line += $"VALUES ({parsed.object_id}, {(uint)STypeInt.???}, {parsed.wdesc.???})" + Environment.NewLine;
                                    //    line = line.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    //    writer.WriteLine(line);
                                    //}
                                    //if ((parsed.wdesc.header2 & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader2.PWD2_Packed_PetOwner) != 0)
                                    //{
                                    //    line = $"{sqlCommand} INTO `ace_object_properties_int` (`aceObjectId`, `intPropertyId`, `propertyValue`)" + Environment.NewLine;
                                    //    line += $"VALUES ({parsed.object_id}, {(uint)STypeInt.???}, {parsed.wdesc._pet_owner})" + Environment.NewLine;
                                    //    line = line.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    //    writer.WriteLine(line);
                                    //}
                                    //if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_Monarch) != 0)
                                    //{
                                    //    line = $"{sqlCommand} INTO `ace_object_properties_int` (`aceObjectId`, `intPropertyId`, `propertyValue`)" + Environment.NewLine;
                                    //    line += $"VALUES ({parsed.object_id}, {(uint)STypeInt.???}, {parsed.wdesc._monarch})" + Environment.NewLine;
                                    //    line = line.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    //    writer.WriteLine(line);
                                    //}
                                    //if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_Workmanship) != 0)
                                    //{
                                    //    line = $"{sqlCommand} INTO `ace_object_properties_int` (`aceObjectId`, `intPropertyId`, `propertyValue`)" + Environment.NewLine;
                                    //    line += $"VALUES ({parsed.object_id}, {(uint)STypeInt.ITEM_WORKMANSHIP_INT}, {parsed.wdesc._workmanship})" + Environment.NewLine;
                                    //    line = line.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    //    writer.WriteLine(line);
                                    //}
                                    if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_PluralName) != 0)
                                        strsLine += $"     , ({guid}, {(uint)STypeString.PLURAL_NAME_STRING}, '{parsed.wdesc._plural_name.m_buffer.Replace("'", "''")}')" + Environment.NewLine;
                                    if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_Priority) != 0)
                                        intsLine += $"     , ({guid}, {(uint)STypeInt.CLOTHING_PRIORITY_INT}, {parsed.wdesc._priority})" + Environment.NewLine;
                                    if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.DEFAULT_SCRIPT_INTENSITY) != 0)
                                        floatsLine += $"     , ({guid}, {(uint)STypeFloat.PHYSICS_SCRIPT_INTENSITY_FLOAT}, {parsed.physicsdesc.default_script_intensity})" + Environment.NewLine;
                                    if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.ELASTICITY) != 0)
                                        floatsLine += $"     , ({guid}, {(uint)STypeFloat.ELASTICITY_FLOAT}, {parsed.physicsdesc.elasticity})" + Environment.NewLine;
                                    if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.FRICTION) != 0)
                                        floatsLine += $"     , ({guid}, {(uint)STypeFloat.FRICTION_FLOAT}, {parsed.physicsdesc.friction})" + Environment.NewLine;
                                    if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.PARENT) != 0)
                                    {
                                        //line = $"{sqlCommand} INTO `ace_object_properties_int` (`aceObjectId`, `intPropertyId`, `propertyValue`)" + Environment.NewLine;
                                        if (!generateNewGuids)
                                            iidsLine += $"     , ({guid}, {(uint)STypeIID.OWNER_IID}, {(uint)parsed.physicsdesc.parent_id})" + Environment.NewLine;

                                        //line += $"VALUES ({parsed.object_id}, {STypeInt.???}, {parsed.physicsdesc.parent_id})" + Environment.NewLine;
                                        //line = line.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                        //writer.WriteLine(line);

                                        //line = $"{sqlCommand} INTO `ace_object_properties_int` (`aceObjectId`, `intPropertyId`, `propertyValue`)" + Environment.NewLine;
                                        intsLine += $"     , ({guid}, {(uint)STypeInt.PARENT_LOCATION_INT}, {parsed.physicsdesc.location_id})" + Environment.NewLine;
                                        //line = line.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                        //writer.WriteLine(line);
                                    }
                                    if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.OBJSCALE) != 0)
                                        floatsLine += $"     , ({guid}, {(uint)STypeFloat.DEFAULT_SCALE_FLOAT}, {parsed.physicsdesc.object_scale})" + Environment.NewLine;

                                    intsLine += $"     , ({guid}, {(uint)STypeInt.PHYSICS_STATE_INT}, {(uint)parsed.physicsdesc.state})" + Environment.NewLine;
                                    ////if (((uint)parsed.physicsdesc.state & (uint)PhysicsState.STATIC_PS) != 0)
                                    ////    boolsLine += $"     , ({parsed.object_id}, {(uint)STypeBool.STUCK_BOOL}, {true})" + Environment.NewLine;
                                    if (((uint)parsed.physicsdesc.state & (uint)PhysicsState.ETHEREAL_PS) != 0)
                                        boolsLine += $"     , ({guid}, {(uint)STypeBool.ETHEREAL_BOOL}, {true})" + Environment.NewLine;
                                    if (((uint)parsed.physicsdesc.state & (uint)PhysicsState.REPORT_COLLISIONS_PS) != 0)
                                        boolsLine += $"     , ({guid}, {(uint)STypeBool.REPORT_COLLISIONS_BOOL}, {true})" + Environment.NewLine;
                                    if (((uint)parsed.physicsdesc.state & (uint)PhysicsState.IGNORE_COLLISIONS_PS) != 0)
                                        boolsLine += $"     , ({guid}, {(uint)STypeBool.IGNORE_COLLISIONS_BOOL}, {true})" + Environment.NewLine;
                                    if (((uint)parsed.physicsdesc.state & (uint)PhysicsState.NODRAW_PS) != 0)
                                        boolsLine += $"     , ({guid}, {(uint)STypeBool.NODRAW_BOOL}, {true})" + Environment.NewLine;
                                    if (((uint)parsed.physicsdesc.state & (uint)PhysicsState.GRAVITY_PS) != 0)
                                        boolsLine += $"     , ({guid}, {(uint)STypeBool.GRAVITY_STATUS_BOOL}, {true})" + Environment.NewLine;
                                    if (((uint)parsed.physicsdesc.state & (uint)PhysicsState.LIGHTING_ON_PS) != 0)
                                        boolsLine += $"     , ({guid}, {(uint)STypeBool.LIGHTS_STATUS_BOOL}, {true})" + Environment.NewLine;
                                    if (((uint)parsed.physicsdesc.state & (uint)PhysicsState.HIDDEN_PS) != 0)
                                        boolsLine += $"     , ({guid}, {(uint)STypeBool.VISIBILITY_BOOL}, {true})" + Environment.NewLine;
                                    if (((uint)parsed.physicsdesc.state & (uint)PhysicsState.SCRIPTED_COLLISION_PS) != 0)
                                        boolsLine += $"     , ({guid}, {(uint)STypeBool.SCRIPTED_COLLISION_BOOL}, {true})" + Environment.NewLine;
                                    if (((uint)parsed.physicsdesc.state & (uint)PhysicsState.INELASTIC_PS) != 0)
                                        boolsLine += $"     , ({guid}, {(uint)STypeBool.INELASTIC_BOOL}, {true})" + Environment.NewLine;
                                    ////if (((uint)parsed.physicsdesc.state & (uint)PhysicsState.CLOAKED_PS) != 0)
                                    ////    boolsLine += $"     , ({parsed.object_id}, {(uint)STypeBool.HIDDEN_ADMIN_BOOL}, {true})" + Environment.NewLine;
                                    if (((uint)parsed.physicsdesc.state & (uint)PhysicsState.REPORT_COLLISIONS_AS_ENVIRONMENT_PS) != 0)
                                        boolsLine += $"     , ({guid}, {(uint)STypeBool.REPORT_COLLISIONS_AS_ENVIRONMENT_BOOL}, {true})" + Environment.NewLine;
                                    if (((uint)parsed.physicsdesc.state & (uint)PhysicsState.EDGE_SLIDE_PS) != 0)
                                        boolsLine += $"     , ({guid}, {(uint)STypeBool.ALLOW_EDGE_SLIDE_BOOL}, {true})" + Environment.NewLine;
                                    if (((uint)parsed.physicsdesc.state & (uint)PhysicsState.FROZEN_PS) != 0)
                                        boolsLine += $"     , ({guid}, {(uint)STypeBool.IS_FROZEN_BOOL}, {true})" + Environment.NewLine;

                                    if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.TRANSLUCENCY) != 0)
                                        floatsLine += $"     , ({guid}, {(uint)STypeFloat.TRANSLUCENCY_FLOAT}, {parsed.physicsdesc.translucency})" + Environment.NewLine;
                                    //if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.VELOCITY) != 0)
                                    //{
                                    //    line = $"{sqlCommand} INTO `ace_object_properties_double` (`aceObjectId`, `dblPropertyId`, `propertyValue`)" + Environment.NewLine;
                                    //    line += $"VALUES ({parsed.object_id}, {(uint)STypeFloat.MAXIMUM_VELOCITY_FLOAT}, {parsed.physicsdesc.velocity})" + Environment.NewLine;
                                    //    line = line.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    //    writer.WriteLine(line);
                                    //}

                                    if (((uint)parsed.wdesc._bitfield & (uint)PublicWeenieDesc.BitfieldIndex.BF_ATTACKABLE) != 0)
                                        boolsLine += $"     , ({guid}, {(uint)STypeBool.ATTACKABLE_BOOL}, {true})" + Environment.NewLine;
                                    if (((uint)parsed.wdesc._bitfield & (uint)PublicWeenieDesc.BitfieldIndex.BF_HIDDEN_ADMIN) != 0)
                                        boolsLine += $"     , ({guid}, {(uint)STypeBool.HIDDEN_ADMIN_BOOL}, {true})" + Environment.NewLine;
                                    if (((uint)parsed.wdesc._bitfield & (uint)PublicWeenieDesc.BitfieldIndex.BF_IMMUNE_CELL_RESTRICTIONS) != 0)
                                        boolsLine += $"     , ({guid}, {(uint)STypeBool.IGNORE_HOUSE_BARRIERS_BOOL}, {true})" + Environment.NewLine;
                                    if (((uint)parsed.wdesc._bitfield & (uint)PublicWeenieDesc.BitfieldIndex.BF_INSCRIBABLE) != 0)
                                        boolsLine += $"     , ({guid}, {(uint)STypeBool.INSCRIBABLE_BOOL}, {true})" + Environment.NewLine;
                                    //if (((uint)parsed.wdesc._bitfield & (uint)PublicWeenieDesc.BitfieldIndex.BF_PLAYER_KILLER) != 0)
                                    //    boolsLine += $"     , ({parsed.object_id}, {(uint)STypeBool.PK_KILLER_BOOL}, {true})" + Environment.NewLine;
                                    if (((uint)parsed.wdesc._bitfield & (uint)PublicWeenieDesc.BitfieldIndex.BF_REQUIRES_PACKSLOT) != 0)
                                        boolsLine += $"     , ({guid}, {(uint)STypeBool.REQUIRES_BACKPACK_SLOT_BOOL}, {true})" + Environment.NewLine;
                                    if (((uint)parsed.wdesc._bitfield & (uint)PublicWeenieDesc.BitfieldIndex.BF_RETAINED) != 0)
                                        boolsLine += $"     , ({guid}, {(uint)STypeBool.RETAINED_BOOL}, {true})" + Environment.NewLine;
                                    if (((uint)parsed.wdesc._bitfield & (uint)PublicWeenieDesc.BitfieldIndex.BF_STUCK) != 0)
                                        boolsLine += $"     , ({guid}, {(uint)STypeBool.STUCK_BOOL}, {true})" + Environment.NewLine;
                                    if (((uint)parsed.wdesc._bitfield & (uint)PublicWeenieDesc.BitfieldIndex.BF_UI_HIDDEN) != 0)
                                        boolsLine += $"     , ({guid}, {(uint)STypeBool.UI_HIDDEN_BOOL}, {true})" + Environment.NewLine;
                                    //if (((uint)parsed.wdesc._bitfield & (uint)PublicWeenieDesc.BitfieldIndex.BF_VENDOR) != 0)
                                    //    boolsLine += $"     , ({parsed.object_id}, {(uint)STypeBool.VENDOR_SERVICE_BOOL}, {true})" + Environment.NewLine;
                                    if (((uint)parsed.wdesc._bitfield & (uint)PublicWeenieDesc.BitfieldIndex.BF_WIELD_LEFT) != 0)
                                        boolsLine += $"     , ({guid}, {(uint)STypeBool.AUTOWIELD_LEFT_BOOL}, {true})" + Environment.NewLine;
                                    if (((uint)parsed.wdesc._bitfield & (uint)PublicWeenieDesc.BitfieldIndex.BF_WIELD_ON_USE) != 0)
                                        boolsLine += $"     , ({guid}, {(uint)STypeBool.WIELD_ON_USE_BOOL}, {true})" + Environment.NewLine;
                                    //if (((uint)parsed.wdesc._bitfield & (uint)PublicWeenieDesc.BitfieldIndex.BF_ADMIN) != 0)
                                    //    boolsLine += $"     , ({parsed.object_id}, {(uint)STypeBool.IS_ADMIN_BOOL}, {true})" + Environment.NewLine;

                                    if (staticObjectsWeenieType.ContainsKey(parsed.object_id))
                                    {
                                        uint weenieType;
                                        staticObjectsWeenieType.TryGetValue(parsed.object_id, out weenieType);
                                        intsLine += $"     , ({guid}, {(uint)9007}, {weenieType})" + Environment.NewLine;
                                    }

                                    if (strsLine != "")
                                    {
                                        strsLine = $"{sqlCommand} INTO `ace_object_properties_string` (`aceObjectId`, `strPropertyId`, `propertyValue`)" + Environment.NewLine
                                            + "VALUES " + strsLine.TrimStart("     ,".ToCharArray());
                                        strsLine = strsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                        writer.WriteLine(strsLine);
                                    }
                                    if (didsLine != "")
                                    {
                                        didsLine = $"{sqlCommand} INTO `ace_object_properties_did` (`aceObjectId`, `didPropertyId`, `propertyValue`)" + Environment.NewLine
                                            + "VALUES " + didsLine.TrimStart("     ,".ToCharArray());
                                        didsLine = didsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                        writer.WriteLine(didsLine);
                                    }
                                    if (iidsLine != "")
                                    {
                                        iidsLine = $"{sqlCommand} INTO `ace_object_properties_iid` (`aceObjectId`, `iidPropertyId`, `propertyValue`)" + Environment.NewLine
                                            + "VALUES " + iidsLine.TrimStart("     ,".ToCharArray());
                                        iidsLine = iidsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                        writer.WriteLine(iidsLine);
                                    }
                                    if (intsLine != "")
                                    {
                                        intsLine = $"{sqlCommand} INTO `ace_object_properties_int` (`aceObjectId`, `intPropertyId`, `propertyValue`)" + Environment.NewLine
                                            + "VALUES " + intsLine.TrimStart("     ,".ToCharArray());
                                        intsLine = intsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                        writer.WriteLine(intsLine);
                                    }
                                    if (bigintsLine != "")
                                    {
                                        bigintsLine = $"{sqlCommand} INTO `ace_object_properties_bigint` (`aceObjectId`, `bigIntPropertyId`, `propertyValue`)" + Environment.NewLine
                                            + "VALUES " + bigintsLine.TrimStart("     ,".ToCharArray());
                                        bigintsLine = bigintsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                        writer.WriteLine(bigintsLine);
                                    }
                                    if (floatsLine != "")
                                    {
                                        floatsLine = $"{sqlCommand} INTO `ace_object_properties_double` (`aceObjectId`, `dblPropertyId`, `propertyValue`)" + Environment.NewLine
                                            + "VALUES " + floatsLine.TrimStart("     ,".ToCharArray());
                                        floatsLine = floatsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                        writer.WriteLine(floatsLine);
                                    }
                                    if (boolsLine != "")
                                    {
                                        boolsLine = $"{sqlCommand} INTO `ace_object_properties_bool` (`aceObjectId`, `boolPropertyId`, `propertyValue`)" + Environment.NewLine
                                            + "VALUES " + boolsLine.TrimStart("     ,".ToCharArray());
                                        boolsLine = boolsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                        writer.WriteLine(boolsLine);
                                    }


                                    bool once = false;
                                    if (parsed.objdesc.subpalettes.Count > 0)
                                    {
                                        line = $"{sqlCommand} INTO `ace_object_palette_change` (`aceObjectId`, `subPaletteId`, `offset`, `length`)" + Environment.NewLine;

                                        foreach (var subPalette in parsed.objdesc.subpalettes)
                                        {
                                            if (once)
                                            {
                                                line += $"     , ({guid}, {subPalette.subID}, {subPalette.offset}, {subPalette.numcolors})" + Environment.NewLine;
                                            }
                                            else
                                            {
                                                line += $"VALUES ({guid}, {subPalette.subID}, {subPalette.offset}, {subPalette.numcolors})" + Environment.NewLine;
                                                once = true;
                                            }
                                        }

                                        line = line.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                        writer.WriteLine(line);
                                    }

                                    once = false;
                                    if (parsed.objdesc.tmChanges.Count > 0)
                                    {
                                        line = $"{sqlCommand} INTO `ace_object_texture_map_change` (`aceObjectId`, `index`, `oldId`, `newId`)" + Environment.NewLine;

                                        foreach (var texture in parsed.objdesc.tmChanges)
                                        {
                                            if (once)
                                            {
                                                line += $"     , ({guid}, {texture.part_index}, {texture.old_tex_id}, {texture.new_tex_id})" + Environment.NewLine;
                                            }
                                            else
                                            {
                                                line += $"VALUES ({guid}, {texture.part_index}, {texture.old_tex_id}, {texture.new_tex_id})" + Environment.NewLine;
                                                once = true;
                                            }
                                        }

                                        line = line.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                        writer.WriteLine(line);
                                    }

                                    once = false;
                                    if (parsed.objdesc.apChanges.Count > 0)
                                    {
                                        line = $"{sqlCommand} INTO `ace_object_animation_change` (`aceObjectId`, `index`, `animationId`)" + Environment.NewLine;

                                        foreach (var animation in parsed.objdesc.apChanges)
                                        {
                                            if (once)
                                            {
                                                line += $"     , ({guid}, {animation.part_index}, {animation.part_id})" + Environment.NewLine;
                                            }
                                            else
                                            {
                                                line += $"VALUES ({guid}, {animation.part_index}, {animation.part_id})" + Environment.NewLine;
                                                once = true;
                                            }
                                        }

                                        line = line.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                        writer.WriteLine(line);
                                    }

                                    if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.POSITION) != 0)
                                    {
                                        line = $"{sqlCommand} INTO `ace_position` (`aceObjectId`, `positionType`, `landblockRaw`, `posX`, `posY`, `posZ`, `qW`, `qX`, `qY`, `qZ`)" + Environment.NewLine +
                                        //line = $"{sqlCommand} INTO `ace_position` (`aceObjectId`, `positionType`, `landblockRaw`, `landblock`, `cell`, `posX`, `posY`, `posZ`, `qW`, `qX`, `qY`, `qZ`)" + Environment.NewLine +
                                        $"VALUES ({guid}, {(uint)STypePosition.LOCATION_POSITION}, {parsed.physicsdesc.pos.objcell_id}, " +
                                        //$"{parsed.physicsdesc.pos.objcell_id >> 16}, {parsed.physicsdesc.pos.objcell_id & 0xFFFF}, " +
                                        $"{parsed.physicsdesc.pos.frame.m_fOrigin.x}, {parsed.physicsdesc.pos.frame.m_fOrigin.y}, {parsed.physicsdesc.pos.frame.m_fOrigin.z}, " +
                                        $"{parsed.physicsdesc.pos.frame.qw}, {parsed.physicsdesc.pos.frame.qx}, {parsed.physicsdesc.pos.frame.qy}, {parsed.physicsdesc.pos.frame.qz});" +
                                        Environment.NewLine;

                                        writer.WriteLine(line);
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine("Unable to export object " + parsed.object_id + ". Exception:" + Environment.NewLine + ex.ToString());
                        }
                    }
                }
            }
        }

        private void WriteStaticObjectData(Dictionary<string, List<CM_Physics.CreateObject>> staticObjects, List<uint> objectIds, string outputFolder, Dictionary<uint, uint> staticObjectsWeenieType)
        {
            string staticFolder = Path.Combine(outputFolder, "6-objects");

            string sqlCommand = "INSERT";
            //string sqlCommand = "REPLACE";

            if (!Directory.Exists(staticFolder))
                Directory.CreateDirectory(staticFolder);

            //Dictionary<ITEM_TYPE, int> fileCount = new Dictionary<ITEM_TYPE, int>();
            Dictionary<string, int> fileCount = new Dictionary<string, int>();

            foreach (string key in staticObjects.Keys)
            {
                //string filename = Path.Combine(staticFolder, $"{key}.sql");

                //if (File.Exists(filename))
                //    File.Delete(filename);

                //if (!fileCount.ContainsKey(key))
                //    fileCount.Add(key, 0);

                ////string fullFile = Path.Combine(staticFolder, $"{filename}");
                //string fullFile = Path.Combine(staticFolder, $"{key}_{fileCount[key]}.sql");

                //if (File.Exists(fullFile))
                //{
                //    FileInfo fi = new FileInfo(fullFile);

                //    // go to the next file if it's bigger than a MB
                //    if (fi.Length > ((1048576) * 40))
                //    {
                //        //fileCount[parsed.wdesc._type]++;
                //        fileCount[key]++;
                //        // fullFile = Path.Combine(staticFolder, $"{parsed.wdesc._type}_{fileCount[parsed.wdesc._type]}.sql");
                //        fullFile = Path.Combine(staticFolder, $"{key}_{fileCount[key]}.sql");

                //        if (File.Exists(fullFile))
                //            File.Delete(fullFile);
                //    }
                //}

                foreach (var parsed in staticObjects[key])
                {
                    try
                    {
                        //if (!fileCount.ContainsKey(key))
                        //    fileCount.Add(key, 0);

                        ////string fullFile = Path.Combine(staticFolder, $"{filename}");
                        //string fullFile = Path.Combine(staticFolder, $"{key}_{fileCount[key]}.sql");

                        //if (File.Exists(fullFile))
                        //{
                        //    FileInfo fi = new FileInfo(fullFile);

                        //    // go to the next file if it's bigger than a MB
                        //    if (fi.Length > ((1048576) * 40))
                        //    {
                        //        //fileCount[parsed.wdesc._type]++;
                        //        fileCount[key]++;
                        //        // fullFile = Path.Combine(staticFolder, $"{parsed.wdesc._type}_{fileCount[parsed.wdesc._type]}.sql");
                        //        fullFile = Path.Combine(staticFolder, $"{key}_{fileCount[key]}.sql");

                        //        if (File.Exists(fullFile))
                        //            File.Delete(fullFile);
                        //    }
                        //}

                        if (!fileCount.ContainsKey(key))
                            fileCount.Add(key, 0);

                        //string fullFile = Path.Combine(staticFolder, $"{filename}");
                        string fullFile = Path.Combine(staticFolder, $"{key}_{fileCount[key]}.sql");

                        if (File.Exists(fullFile))
                        {
                            FileInfo fi = new FileInfo(fullFile);

                            // go to the next file if it's bigger than a MB
                            if (fi.Length > ((1048576) * 40))
                            {
                                //fileCount[parsed.wdesc._type]++;
                                fileCount[key]++;
                                // fullFile = Path.Combine(staticFolder, $"{parsed.wdesc._type}_{fileCount[parsed.wdesc._type]}.sql");
                                fullFile = Path.Combine(staticFolder, $"{key}_{fileCount[key]}.sql");

                                if (File.Exists(fullFile))
                                    File.Delete(fullFile);
                            }
                        }

                        using (FileStream fs = new FileStream(fullFile, FileMode.Append))
                        {
                            using (StreamWriter writer = new StreamWriter(fs))
                            {
                                string intsLine = "", bigintsLine = "", floatsLine = "", boolsLine = "", strsLine = "", didsLine = "", iidsLine = "";

                                string line = $"{sqlCommand} INTO `ace_object` (`" +
                                "aceObjectId`, `aceObjectDescriptionFlags`, " +
                                "`weenieClassId`, `weenieHeaderFlags`, " +
                                "`currentMotionState`, " +
                                "`physicsDescriptionFlag`" +
                                ")" + Environment.NewLine + "VALUES (" +

                                $"{parsed.object_id}, {parsed.wdesc._bitfield}, " +
                                $"{parsed.wdesc._wcid}, {parsed.wdesc.header}, "; //+
                                didsLine += $"     , ({parsed.object_id}, {(uint)STypeDID.ICON_DID}, {(uint)parsed.wdesc._iconID})" + Environment.NewLine;
                                if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_IconOverlay) != 0)
                                    didsLine += $"     , ({parsed.object_id}, {(uint)STypeDID.ICON_OVERLAY_DID}, {(uint)parsed.wdesc._iconOverlayID})" + Environment.NewLine;
                                if ((parsed.wdesc.header2 & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader2.PWD2_Packed_IconUnderlay) != 0)
                                    didsLine += $"     , ({parsed.object_id}, {(uint)STypeDID.ICON_UNDERLAY_DID}, {(uint)parsed.wdesc._iconUnderlayID})" + Environment.NewLine;

                                if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.CSetup) != 0)
                                    didsLine += $"     , ({parsed.object_id}, {(uint)STypeDID.SETUP_DID}, {(uint)parsed.physicsdesc.setup_id})" + Environment.NewLine;
                                if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.STABLE) != 0)
                                    didsLine += $"     , ({parsed.object_id}, {(uint)STypeDID.SOUND_TABLE_DID}, {(uint)parsed.physicsdesc.stable_id})" + Environment.NewLine;
                                if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.MTABLE) != 0)
                                    didsLine += $"     , ({parsed.object_id}, {(uint)STypeDID.MOTION_TABLE_DID}, {(uint)parsed.physicsdesc.mtable_id})" + Environment.NewLine;
                                if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.MOVEMENT) != 0)
                                    line += $"'{ConvertMovementBufferToString(parsed.physicsdesc.movement_buffer)}', ";
                                else
                                    line += $"NULL, ";
                                if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.ANIMFRAME_ID) != 0)
                                    intsLine += $"     , ({parsed.object_id}, {(uint)STypeInt.PLACEMENT_POSITION_INT}, {(uint)parsed.physicsdesc.animframe_id})" + Environment.NewLine;
                                if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.PETABLE) != 0)
                                    didsLine += $"     , ({parsed.object_id}, {(uint)STypeDID.PHYSICS_EFFECT_TABLE_DID}, {(uint)parsed.physicsdesc.phstable_id})" + Environment.NewLine;
                                line += $"{parsed.physicsdesc.bitfield}";
                                if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_SpellID) != 0)
                                    didsLine += $"     , ({parsed.object_id}, {(uint)STypeDID.SPELL_DID}, {(uint)parsed.wdesc._spellID})" + Environment.NewLine;
                                if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_PScript) != 0)
                                    didsLine += $"     , ({parsed.object_id}, {(uint)STypeDID.PHYSICS_SCRIPT_DID}, {(uint)parsed.wdesc._pscript})" + Environment.NewLine;
                                if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.DEFAULT_SCRIPT) != 0)
                                    didsLine += $"     , ({parsed.object_id}, {(uint)STypeDID.USE_USER_ANIMATION_DID}, {(uint)parsed.physicsdesc.default_script})" + Environment.NewLine;

                                line += ");" + Environment.NewLine;

                                writer.WriteLine(line);

                                if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.POSITION) != 0)
                                {
                                    line = $"{sqlCommand} INTO `ace_position` (`aceObjectId`, `positionType`, `landblockRaw`, `posX`, `posY`, `posZ`, `qW`, `qX`, `qY`, `qZ`)" + Environment.NewLine +
                                    //line = $"{sqlCommand} INTO `ace_position` (`aceObjectId`, `positionType`, `landblockRaw`, `landblock`, `cell`, `posX`, `posY`, `posZ`, `qW`, `qX`, `qY`, `qZ`)" + Environment.NewLine +
                                    $"VALUES ({parsed.object_id}, {(uint)STypePosition.LOCATION_POSITION}, {parsed.physicsdesc.pos.objcell_id}, " +
                                    //$"{parsed.physicsdesc.pos.objcell_id >> 16}, {parsed.physicsdesc.pos.objcell_id & 0xFFFF}, " +
                                    $"{parsed.physicsdesc.pos.frame.m_fOrigin.x}, {parsed.physicsdesc.pos.frame.m_fOrigin.y}, {parsed.physicsdesc.pos.frame.m_fOrigin.z}, " +
                                    $"{parsed.physicsdesc.pos.frame.qw}, {parsed.physicsdesc.pos.frame.qx}, {parsed.physicsdesc.pos.frame.qy}, {parsed.physicsdesc.pos.frame.qz});" +
                                    Environment.NewLine;

                                    writer.WriteLine(line);
                                }
                                                                
                                strsLine += $"     , ({parsed.object_id}, {(uint)STypeString.NAME_STRING}, '{parsed.wdesc._name.m_buffer.Replace("'", "''")}')" + Environment.NewLine;

                                intsLine += $"     , ({parsed.object_id}, {(uint)STypeInt.ITEM_TYPE_INT}, {(uint)parsed.wdesc._type})" + Environment.NewLine;

                                if (parsed.objdesc.subpalettes.Count > 0)
                                    //intsLine += $"     , ({parsed.object_id}, {(uint)STypeInt.PALETTE_TEMPLATE_INT}, {parsed.objdesc.paletteID})" + Environment.NewLine;
                                    didsLine += $"     , ({parsed.object_id}, {(uint)STypeDID.PALETTE_BASE_DID}, {(uint)parsed.objdesc.paletteID})" + Environment.NewLine;

                                if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_AmmoType) != 0)
                                    intsLine += $"     , ({parsed.object_id}, {(uint)STypeInt.AMMO_TYPE_INT}, {(uint)parsed.wdesc._ammoType})" + Environment.NewLine;
                                if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_BlipColor) != 0)
                                    intsLine += $"     , ({parsed.object_id}, {(uint)STypeInt.RADARBLIP_COLOR_INT}, {parsed.wdesc._blipColor})" + Environment.NewLine;
                                if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_Burden) != 0)
                                    intsLine += $"     , ({parsed.object_id}, {(uint)STypeInt.ENCUMB_VAL_INT}, {parsed.wdesc._burden})" + Environment.NewLine;
                                if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_CombatUse) != 0)
                                    intsLine += $"     , ({parsed.object_id}, {(uint)STypeInt.COMBAT_USE_INT}, {parsed.wdesc._combatUse})" + Environment.NewLine;
                                if ((parsed.wdesc.header2 & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader2.PWD2_Packed_CooldownDuration) != 0)
                                    floatsLine += $"     , ({parsed.object_id}, {(uint)STypeFloat.COOLDOWN_DURATION_FLOAT}, {parsed.wdesc._cooldown_duration})" + Environment.NewLine;
                                if ((parsed.wdesc.header2 & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader2.PWD2_Packed_CooldownID) != 0)
                                    intsLine += $"     , ({parsed.object_id}, {(uint)STypeInt.SHARED_COOLDOWN_INT}, {parsed.wdesc._cooldown_id})" + Environment.NewLine;
                                if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_UIEffects) != 0)
                                    intsLine += $"     , ({parsed.object_id}, {(uint)STypeInt.UI_EFFECTS_INT}, {parsed.wdesc._effects})" + Environment.NewLine;
                                if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_ContainersCapacity) != 0)
                                    intsLine += $"     , ({parsed.object_id}, {(uint)STypeInt.CONTAINERS_CAPACITY_INT}, {parsed.wdesc._containersCapacity})" + Environment.NewLine;
                                if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_HookType) != 0)
                                    intsLine += $"     , ({parsed.object_id}, {(uint)STypeInt.HOOK_TYPE_INT}, {(uint)parsed.wdesc._hook_type})" + Environment.NewLine;
                                if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_HookItemTypes) != 0)
                                    intsLine += $"     , ({parsed.object_id}, {(uint)STypeInt.HOOK_ITEM_TYPE_INT}, {parsed.wdesc._hook_item_types})" + Environment.NewLine;
                                if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_ItemsCapacity) != 0)
                                    intsLine += $"     , ({parsed.object_id}, {(uint)STypeInt.ITEMS_CAPACITY_INT}, {parsed.wdesc._itemsCapacity})" + Environment.NewLine;
                                if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_Location) != 0)
                                    intsLine += $"     , ({parsed.object_id}, {(uint)STypeInt.CURRENT_WIELDED_LOCATION_INT}, {parsed.wdesc._location})" + Environment.NewLine;
                                if ((parsed.wdesc.header & unchecked((uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_MaterialType)) != 0)
                                    intsLine += $"     , ({parsed.object_id}, {(uint)STypeInt.MATERIAL_TYPE_INT}, {(uint)parsed.wdesc._material_type})" + Environment.NewLine;
                                if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_MaxStackSize) != 0)
                                    intsLine += $"     , ({parsed.object_id}, {(uint)STypeInt.MAX_STACK_SIZE_INT}, {parsed.wdesc._maxStackSize})" + Environment.NewLine;
                                if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_MaxStructure) != 0)
                                    intsLine += $"     , ({parsed.object_id}, {(uint)STypeInt.MAX_STRUCTURE_INT}, {parsed.wdesc._maxStructure})" + Environment.NewLine;
                                if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_RadarEnum) != 0)
                                    intsLine += $"     , ({parsed.object_id}, {(uint)STypeInt.SHOWABLE_ON_RADAR_INT}, {(uint)parsed.wdesc._radar_enum})" + Environment.NewLine;
                                if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_StackSize) != 0)
                                    intsLine += $"     , ({parsed.object_id}, {(uint)STypeInt.STACK_SIZE_INT}, {parsed.wdesc._stackSize})" + Environment.NewLine;
                                if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_Structure) != 0)
                                    intsLine += $"     , ({parsed.object_id}, {(uint)STypeInt.STRUCTURE_INT}, {parsed.wdesc._structure})" + Environment.NewLine;
                                if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_TargetType) != 0)
                                    intsLine += $"     , ({parsed.object_id}, {(uint)STypeInt.TARGET_TYPE_INT}, {(uint)parsed.wdesc._targetType})" + Environment.NewLine;
                                if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_Useability) != 0)
                                    intsLine += $"     , ({parsed.object_id}, {(uint)STypeInt.ITEM_USEABLE_INT}, {(uint)parsed.wdesc._useability})" + Environment.NewLine;
                                if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_UseRadius) != 0)
                                    floatsLine += $"     , ({parsed.object_id}, {(uint)STypeFloat.USE_RADIUS_FLOAT}, {parsed.wdesc._useRadius})" + Environment.NewLine;
                                if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_ValidLocations) != 0)
                                    intsLine += $"     , ({parsed.object_id}, {(uint)STypeInt.LOCATIONS_INT}, {parsed.wdesc._valid_locations})" + Environment.NewLine;
                                if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_Value) != 0)
                                    intsLine += $"     , ({parsed.object_id}, {(uint)STypeInt.VALUE_INT}, {parsed.wdesc._value})" + Environment.NewLine;
                                //if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_ContainerID) != 0)
                                if (((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_ContainerID) != 0) && objectIds.Contains(parsed.wdesc._containerID))
                                    iidsLine += $"     , ({parsed.object_id}, {(uint)STypeIID.CONTAINER_IID}, {(uint)parsed.wdesc._containerID})" + Environment.NewLine;
                                //{
                                //    line = $"{sqlCommand} INTO `ace_object_properties_int` (`aceObjectId`, `intPropertyId`, `propertyValue`)" + Environment.NewLine;
                                //    line += $"VALUES ({parsed.object_id}, {(uint)STypeInt.???}, {parsed.wdesc._containerID})" + Environment.NewLine;
                                //    line = line.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                //    writer.WriteLine(line);
                                //}
                                //if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_WielderID) != 0)
                                //{
                                //    line = $"{sqlCommand} INTO `ace_object_properties_int` (`aceObjectId`, `intPropertyId`, `propertyValue`)" + Environment.NewLine;
                                //    line += $"VALUES ({parsed.object_id}, {(uint)STypeInt.???}, {parsed.wdesc._wielderID})" + Environment.NewLine;
                                //    line = line.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                //    writer.WriteLine(line);
                                //}
                                //if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_WielderID) != 0)
                                //    intsLine += $"     , ({parsed.object_id}, {(uint)STypeIID.WIELDER_IID}, {parsed.wdesc._wielderID})" + Environment.NewLine;
                                if (((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_WielderID) != 0) && objectIds.Contains(parsed.wdesc._wielderID))
                                    iidsLine += $"     , ({parsed.object_id}, {(uint)STypeIID.WIELDER_IID}, {(uint)parsed.wdesc._wielderID})" + Environment.NewLine;
                                //if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_HouseOwner) != 0)
                                //{
                                //    line = $"{sqlCommand} INTO `ace_object_properties_int` (`aceObjectId`, `intPropertyId`, `propertyValue`)" + Environment.NewLine;
                                //    line += $"VALUES ({parsed.object_id}, {(uint)STypeInt.???}, {parsed.wdesc._house_owner_iid})" + Environment.NewLine;
                                //    line = line.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                //    writer.WriteLine(line);
                                //}
                                //if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_HouseRestrictions) != 0)
                                //{
                                //    line = $"{sqlCommand} INTO `ace_object_properties_int` (`aceObjectId`, `intPropertyId`, `propertyValue`)" + Environment.NewLine;
                                //    line += $"VALUES ({parsed.object_id}, {(uint)STypeInt.???}, {parsed.wdesc.???})" + Environment.NewLine;
                                //    line = line.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                //    writer.WriteLine(line);
                                //}
                                //if ((parsed.wdesc.header2 & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader2.PWD2_Packed_PetOwner) != 0)
                                //{
                                //    line = $"{sqlCommand} INTO `ace_object_properties_int` (`aceObjectId`, `intPropertyId`, `propertyValue`)" + Environment.NewLine;
                                //    line += $"VALUES ({parsed.object_id}, {(uint)STypeInt.???}, {parsed.wdesc._pet_owner})" + Environment.NewLine;
                                //    line = line.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                //    writer.WriteLine(line);
                                //}
                                //if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_Monarch) != 0)
                                //{
                                //    line = $"{sqlCommand} INTO `ace_object_properties_int` (`aceObjectId`, `intPropertyId`, `propertyValue`)" + Environment.NewLine;
                                //    line += $"VALUES ({parsed.object_id}, {(uint)STypeInt.???}, {parsed.wdesc._monarch})" + Environment.NewLine;
                                //    line = line.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                //    writer.WriteLine(line);
                                //}
                                //if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_Workmanship) != 0)
                                //{
                                //    line = $"{sqlCommand} INTO `ace_object_properties_int` (`aceObjectId`, `intPropertyId`, `propertyValue`)" + Environment.NewLine;
                                //    line += $"VALUES ({parsed.object_id}, {(uint)STypeInt.ITEM_WORKMANSHIP_INT}, {parsed.wdesc._workmanship})" + Environment.NewLine;
                                //    line = line.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                //    writer.WriteLine(line);
                                //}
                                if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_PluralName) != 0)
                                    strsLine += $"     , ({parsed.object_id}, {(uint)STypeString.PLURAL_NAME_STRING}, '{parsed.wdesc._plural_name.m_buffer.Replace("'", "''")}')" + Environment.NewLine;
                                if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_Priority) != 0)
                                    intsLine += $"     , ({parsed.object_id}, {(uint)STypeInt.CLOTHING_PRIORITY_INT}, {parsed.wdesc._priority})" + Environment.NewLine;
                                if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.DEFAULT_SCRIPT_INTENSITY) != 0)
                                    floatsLine += $"     , ({parsed.object_id}, {(uint)STypeFloat.PHYSICS_SCRIPT_INTENSITY_FLOAT}, {parsed.physicsdesc.default_script_intensity})" + Environment.NewLine;
                                if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.ELASTICITY) != 0)
                                    floatsLine += $"     , ({parsed.object_id}, {(uint)STypeFloat.ELASTICITY_FLOAT}, {parsed.physicsdesc.elasticity})" + Environment.NewLine;
                                if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.FRICTION) != 0)
                                    floatsLine += $"     , ({parsed.object_id}, {(uint)STypeFloat.FRICTION_FLOAT}, {parsed.physicsdesc.friction})" + Environment.NewLine;
                                if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.PARENT) != 0)
                                {
                                    //line = $"{sqlCommand} INTO `ace_object_properties_int` (`aceObjectId`, `intPropertyId`, `propertyValue`)" + Environment.NewLine;
                                    iidsLine += $"     , ({parsed.object_id}, {(uint)STypeIID.OWNER_IID}, {(uint)parsed.physicsdesc.parent_id})" + Environment.NewLine;
                                    //line += $"VALUES ({parsed.object_id}, {STypeInt.???}, {parsed.physicsdesc.parent_id})" + Environment.NewLine;
                                    //line = line.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    //writer.WriteLine(line);

                                    //line = $"{sqlCommand} INTO `ace_object_properties_int` (`aceObjectId`, `intPropertyId`, `propertyValue`)" + Environment.NewLine;
                                    intsLine += $"     , ({parsed.object_id}, {(uint)STypeInt.PARENT_LOCATION_INT}, {parsed.physicsdesc.location_id})" + Environment.NewLine;
                                    //line = line.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    //writer.WriteLine(line);
                                }
                                if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.OBJSCALE) != 0)
                                    floatsLine += $"     , ({parsed.object_id}, {(uint)STypeFloat.DEFAULT_SCALE_FLOAT}, {parsed.physicsdesc.object_scale})" + Environment.NewLine;

                                intsLine += $"     , ({parsed.object_id}, {(uint)STypeInt.PHYSICS_STATE_INT}, {(uint)parsed.physicsdesc.state})" + Environment.NewLine;
                                ////if (((uint)parsed.physicsdesc.state & (uint)PhysicsState.STATIC_PS) != 0)
                                ////    boolsLine += $"     , ({parsed.object_id}, {(uint)STypeBool.STUCK_BOOL}, {true})" + Environment.NewLine;
                                if (((uint)parsed.physicsdesc.state & (uint)PhysicsState.ETHEREAL_PS) != 0)
                                    boolsLine += $"     , ({parsed.object_id}, {(uint)STypeBool.ETHEREAL_BOOL}, {true})" + Environment.NewLine;
                                if (((uint)parsed.physicsdesc.state & (uint)PhysicsState.REPORT_COLLISIONS_PS) != 0)
                                    boolsLine += $"     , ({parsed.object_id}, {(uint)STypeBool.REPORT_COLLISIONS_BOOL}, {true})" + Environment.NewLine;
                                if (((uint)parsed.physicsdesc.state & (uint)PhysicsState.IGNORE_COLLISIONS_PS) != 0)
                                    boolsLine += $"     , ({parsed.object_id}, {(uint)STypeBool.IGNORE_COLLISIONS_BOOL}, {true})" + Environment.NewLine;
                                if (((uint)parsed.physicsdesc.state & (uint)PhysicsState.NODRAW_PS) != 0)
                                    boolsLine += $"     , ({parsed.object_id}, {(uint)STypeBool.NODRAW_BOOL}, {true})" + Environment.NewLine;
                                if (((uint)parsed.physicsdesc.state & (uint)PhysicsState.GRAVITY_PS) != 0)
                                    boolsLine += $"     , ({parsed.object_id}, {(uint)STypeBool.GRAVITY_STATUS_BOOL}, {true})" + Environment.NewLine;
                                if (((uint)parsed.physicsdesc.state & (uint)PhysicsState.LIGHTING_ON_PS) != 0)
                                    boolsLine += $"     , ({parsed.object_id}, {(uint)STypeBool.LIGHTS_STATUS_BOOL}, {true})" + Environment.NewLine;
                                if (((uint)parsed.physicsdesc.state & (uint)PhysicsState.HIDDEN_PS) != 0)
                                    boolsLine += $"     , ({parsed.object_id}, {(uint)STypeBool.VISIBILITY_BOOL}, {true})" + Environment.NewLine;
                                if (((uint)parsed.physicsdesc.state & (uint)PhysicsState.SCRIPTED_COLLISION_PS) != 0)
                                    boolsLine += $"     , ({parsed.object_id}, {(uint)STypeBool.SCRIPTED_COLLISION_BOOL}, {true})" + Environment.NewLine;
                                if (((uint)parsed.physicsdesc.state & (uint)PhysicsState.INELASTIC_PS) != 0)
                                    boolsLine += $"     , ({parsed.object_id}, {(uint)STypeBool.INELASTIC_BOOL}, {true})" + Environment.NewLine;
                                ////if (((uint)parsed.physicsdesc.state & (uint)PhysicsState.CLOAKED_PS) != 0)
                                ////    boolsLine += $"     , ({parsed.object_id}, {(uint)STypeBool.HIDDEN_ADMIN_BOOL}, {true})" + Environment.NewLine;
                                if (((uint)parsed.physicsdesc.state & (uint)PhysicsState.REPORT_COLLISIONS_AS_ENVIRONMENT_PS) != 0)
                                    boolsLine += $"     , ({parsed.object_id}, {(uint)STypeBool.REPORT_COLLISIONS_AS_ENVIRONMENT_BOOL}, {true})" + Environment.NewLine;
                                if (((uint)parsed.physicsdesc.state & (uint)PhysicsState.EDGE_SLIDE_PS) != 0)
                                    boolsLine += $"     , ({parsed.object_id}, {(uint)STypeBool.ALLOW_EDGE_SLIDE_BOOL}, {true})" + Environment.NewLine;
                                if (((uint)parsed.physicsdesc.state & (uint)PhysicsState.FROZEN_PS) != 0)
                                    boolsLine += $"     , ({parsed.object_id}, {(uint)STypeBool.IS_FROZEN_BOOL}, {true})" + Environment.NewLine;

                                if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.TRANSLUCENCY) != 0)
                                    floatsLine += $"     , ({parsed.object_id}, {(uint)STypeFloat.TRANSLUCENCY_FLOAT}, {parsed.physicsdesc.translucency})" + Environment.NewLine;
                                //if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.VELOCITY) != 0)
                                //{
                                //    line = $"{sqlCommand} INTO `ace_object_properties_double` (`aceObjectId`, `dblPropertyId`, `propertyValue`)" + Environment.NewLine;
                                //    line += $"VALUES ({parsed.object_id}, {(uint)STypeFloat.MAXIMUM_VELOCITY_FLOAT}, {parsed.physicsdesc.velocity})" + Environment.NewLine;
                                //    line = line.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                //    writer.WriteLine(line);
                                //}

                                if (((uint)parsed.wdesc._bitfield & (uint)PublicWeenieDesc.BitfieldIndex.BF_ATTACKABLE) != 0)
                                    boolsLine += $"     , ({parsed.object_id}, {(uint)STypeBool.ATTACKABLE_BOOL}, {true})" + Environment.NewLine;
                                if (((uint)parsed.wdesc._bitfield & (uint)PublicWeenieDesc.BitfieldIndex.BF_HIDDEN_ADMIN) != 0)
                                    boolsLine += $"     , ({parsed.object_id}, {(uint)STypeBool.HIDDEN_ADMIN_BOOL}, {true})" + Environment.NewLine;
                                if (((uint)parsed.wdesc._bitfield & (uint)PublicWeenieDesc.BitfieldIndex.BF_IMMUNE_CELL_RESTRICTIONS) != 0)
                                    boolsLine += $"     , ({parsed.object_id}, {(uint)STypeBool.IGNORE_HOUSE_BARRIERS_BOOL}, {true})" + Environment.NewLine;
                                if (((uint)parsed.wdesc._bitfield & (uint)PublicWeenieDesc.BitfieldIndex.BF_INSCRIBABLE) != 0)
                                    boolsLine += $"     , ({parsed.object_id}, {(uint)STypeBool.INSCRIBABLE_BOOL}, {true})" + Environment.NewLine;
                                //if (((uint)parsed.wdesc._bitfield & (uint)PublicWeenieDesc.BitfieldIndex.BF_PLAYER_KILLER) != 0)
                                //    boolsLine += $"     , ({parsed.object_id}, {(uint)STypeBool.PK_KILLER_BOOL}, {true})" + Environment.NewLine;
                                if (((uint)parsed.wdesc._bitfield & (uint)PublicWeenieDesc.BitfieldIndex.BF_REQUIRES_PACKSLOT) != 0)
                                    boolsLine += $"     , ({parsed.object_id}, {(uint)STypeBool.REQUIRES_BACKPACK_SLOT_BOOL}, {true})" + Environment.NewLine;
                                if (((uint)parsed.wdesc._bitfield & (uint)PublicWeenieDesc.BitfieldIndex.BF_RETAINED) != 0)
                                    boolsLine += $"     , ({parsed.object_id}, {(uint)STypeBool.RETAINED_BOOL}, {true})" + Environment.NewLine;
                                if (((uint)parsed.wdesc._bitfield & (uint)PublicWeenieDesc.BitfieldIndex.BF_STUCK) != 0)
                                    boolsLine += $"     , ({parsed.object_id}, {(uint)STypeBool.STUCK_BOOL}, {true})" + Environment.NewLine;
                                if (((uint)parsed.wdesc._bitfield & (uint)PublicWeenieDesc.BitfieldIndex.BF_UI_HIDDEN) != 0)
                                    boolsLine += $"     , ({parsed.object_id}, {(uint)STypeBool.UI_HIDDEN_BOOL}, {true})" + Environment.NewLine;
                                //if (((uint)parsed.wdesc._bitfield & (uint)PublicWeenieDesc.BitfieldIndex.BF_VENDOR) != 0)
                                //    boolsLine += $"     , ({parsed.object_id}, {(uint)STypeBool.VENDOR_SERVICE_BOOL}, {true})" + Environment.NewLine;
                                if (((uint)parsed.wdesc._bitfield & (uint)PublicWeenieDesc.BitfieldIndex.BF_WIELD_LEFT) != 0)
                                    boolsLine += $"     , ({parsed.object_id}, {(uint)STypeBool.AUTOWIELD_LEFT_BOOL}, {true})" + Environment.NewLine;
                                if (((uint)parsed.wdesc._bitfield & (uint)PublicWeenieDesc.BitfieldIndex.BF_WIELD_ON_USE) != 0)
                                    boolsLine += $"     , ({parsed.object_id}, {(uint)STypeBool.WIELD_ON_USE_BOOL}, {true})" + Environment.NewLine;
                                //if (((uint)parsed.wdesc._bitfield & (uint)PublicWeenieDesc.BitfieldIndex.BF_ADMIN) != 0)
                                //    boolsLine += $"     , ({parsed.object_id}, {(uint)STypeBool.IS_ADMIN_BOOL}, {true})" + Environment.NewLine;

                                if (staticObjectsWeenieType.ContainsKey(parsed.object_id))
                                {
                                    uint weenieType;
                                    staticObjectsWeenieType.TryGetValue(parsed.object_id, out weenieType);
                                    intsLine += $"     , ({parsed.object_id}, {(uint)9007}, {weenieType})" + Environment.NewLine;
                                }
                                    
                                if (strsLine != "")
                                {
                                    strsLine = $"{sqlCommand} INTO `ace_object_properties_string` (`aceObjectId`, `strPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + strsLine.TrimStart("     ,".ToCharArray());
                                    strsLine = strsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(strsLine);
                                }
                                if (didsLine != "")
                                {
                                    didsLine = $"{sqlCommand} INTO `ace_object_properties_did` (`aceObjectId`, `didPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + didsLine.TrimStart("     ,".ToCharArray());
                                    didsLine = didsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(didsLine);
                                }
                                if (iidsLine != "")
                                {
                                    iidsLine = $"{sqlCommand} INTO `ace_object_properties_iid` (`aceObjectId`, `iidPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + iidsLine.TrimStart("     ,".ToCharArray());
                                    iidsLine = iidsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(iidsLine);
                                }
                                if (intsLine != "")
                                {
                                    intsLine = $"{sqlCommand} INTO `ace_object_properties_int` (`aceObjectId`, `intPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + intsLine.TrimStart("     ,".ToCharArray());
                                    intsLine = intsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(intsLine);
                                }
                                if (bigintsLine != "")
                                {
                                    bigintsLine = $"{sqlCommand} INTO `ace_object_properties_bigint` (`aceObjectId`, `bigIntPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + bigintsLine.TrimStart("     ,".ToCharArray());
                                    bigintsLine = bigintsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(bigintsLine);
                                }
                                if (floatsLine != "")
                                {
                                    floatsLine = $"{sqlCommand} INTO `ace_object_properties_double` (`aceObjectId`, `dblPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + floatsLine.TrimStart("     ,".ToCharArray());
                                    floatsLine = floatsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(floatsLine);
                                }
                                if (boolsLine != "")
                                {
                                    boolsLine = $"{sqlCommand} INTO `ace_object_properties_bool` (`aceObjectId`, `boolPropertyId`, `propertyValue`)" + Environment.NewLine
                                        + "VALUES " + boolsLine.TrimStart("     ,".ToCharArray());
                                    boolsLine = boolsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(boolsLine);
                                }


                                bool once = false;
                                if (parsed.objdesc.subpalettes.Count > 0)
                                {
                                    line = $"{sqlCommand} INTO `ace_object_palette_change` (`aceObjectId`, `subPaletteId`, `offset`, `length`)" + Environment.NewLine;

                                    foreach (var subPalette in parsed.objdesc.subpalettes)
                                    {
                                        if (once)
                                        {
                                            line += $"     , ({parsed.object_id}, {subPalette.subID}, {subPalette.offset}, {subPalette.numcolors})" + Environment.NewLine;
                                        }
                                        else
                                        {
                                            line += $"VALUES ({parsed.object_id}, {subPalette.subID}, {subPalette.offset}, {subPalette.numcolors})" + Environment.NewLine;
                                            once = true;
                                        }
                                    }

                                    line = line.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(line);
                                }

                                once = false;
                                if (parsed.objdesc.tmChanges.Count > 0)
                                {
                                    line = $"{sqlCommand} INTO `ace_object_texture_map_change` (`aceObjectId`, `index`, `oldId`, `newId`)" + Environment.NewLine;

                                    foreach (var texture in parsed.objdesc.tmChanges)
                                    {
                                        if (once)
                                        {
                                            line += $"     , ({parsed.object_id}, {texture.part_index}, {texture.old_tex_id}, {texture.new_tex_id})" + Environment.NewLine;
                                        }
                                        else
                                        {
                                            line += $"VALUES ({parsed.object_id}, {texture.part_index}, {texture.old_tex_id}, {texture.new_tex_id})" + Environment.NewLine;
                                            once = true;
                                        }
                                    }

                                    line = line.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(line);
                                }

                                once = false;
                                if (parsed.objdesc.apChanges.Count > 0)
                                {
                                    line = $"{sqlCommand} INTO `ace_object_animation_change` (`aceObjectId`, `index`, `animationId`)" + Environment.NewLine;

                                    foreach (var animation in parsed.objdesc.apChanges)
                                    {
                                        if (once)
                                        {
                                            line += $"     , ({parsed.object_id}, {animation.part_index}, {animation.part_id})" + Environment.NewLine;
                                        }
                                        else
                                        {
                                            line += $"VALUES ({parsed.object_id}, {animation.part_index}, {animation.part_id})" + Environment.NewLine;
                                            once = true;
                                        }
                                    }

                                    line = line.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                    writer.WriteLine(line);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("Unable to export object " + parsed.object_id + ". Exception:" + Environment.NewLine + ex.ToString());
                    }
                }
            }
        }

        private void WriteWeenieFiles(Dictionary<string, List<CM_Physics.CreateObject>> weenies, string outputFolder, Dictionary<uint, uint> weeniesWeenieType)
        {
            string templateFolder = Path.Combine(outputFolder, "1-weenies");

            string sqlCommand = "INSERT";
            //string sqlCommand = "REPLACE";

            if (!Directory.Exists(templateFolder))
                Directory.CreateDirectory(templateFolder);

            foreach (string key in weenies.Keys)
            {
                //string filename = Path.Combine(templateFolder, $"{key}.sql");

                //if (File.Exists(filename))
                //    File.Delete(filename);

                foreach (var parsed in weenies[key])
                {
                    bool once = false;

                    //if (File.Exists(fullFile))
                    //{
                    //    FileInfo fi = new FileInfo(fullFile);

                    //    // go to the next file if it's bigger than a MB
                    //    if (fi.Length > ((1048576) * 40))
                    //    {
                    //        fileCount[parsed.wdesc._type]++;
                    //        fullFile = Path.Combine(staticFolder, $"{parsed.wdesc._type}_{fileCount[parsed.wdesc._type]}.sql");
                    //    }
                    //}

                    string keyFolder = Path.Combine(templateFolder, key);

                    if (!Directory.Exists(keyFolder))
                        Directory.CreateDirectory(keyFolder);

                    string filename = Path.Combine(keyFolder, $"{parsed.wdesc._wcid}.sql");

                    if (key == "CreaturesUnsorted")
                    {
                        // do magical name sorting here.. 
                    }

                    if (File.Exists(filename))
                        File.Delete(filename);

                    using (FileStream fs = new FileStream(filename, FileMode.Append))
                    {
                        using (StreamWriter writer = new StreamWriter(fs))
                        {
                            string weenieName = "";
                            if (Enum.IsDefined(typeof(WeenieClasses), (ushort)parsed.wdesc._wcid))
                            {
                                weenieName = Enum.GetName(typeof(WeenieClasses), parsed.wdesc._wcid).Substring(2);
                                //weenieName = weenieName.Substring(0, weenieName.Length - 6).Replace("_", "").ToLower();
                                weenieName = weenieName.Substring(0, weenieName.Length - 6).Replace("_", "-").ToLower();
                            }
                            else
                                weenieName = "ace" + parsed.wdesc._wcid.ToString() + "-" + parsed.wdesc._name.m_buffer.Replace("'", "").Replace(" ", "").Replace(".", "").Replace("(", "").Replace(")", "").Replace("+", "").Replace(":", "").Replace("_", "").Replace("-", "").Replace(",", "").ToLower();

                            string line = $"/* Weenie - {key} - {parsed.wdesc._name.m_buffer} ({parsed.wdesc._wcid}) */" + Environment.NewLine;

                            line += $"DELETE FROM ace_weenie_class WHERE weenieClassId = {parsed.wdesc._wcid};" + Environment.NewLine + Environment.NewLine;

                            line += $"{sqlCommand} INTO ace_weenie_class (`weenieClassId`, `weenieClassDescription`)" + Environment.NewLine +
                                   $"VALUES ({parsed.wdesc._wcid}, '{weenieName}');" + Environment.NewLine;
                            writer.WriteLine(line);

                            string intsLine = "", bigintsLine = "", floatsLine = "", boolsLine = "", strsLine = "", didsLine = "", iidsLine = "";

                            line = $"{sqlCommand} INTO `ace_object` (`" +
                                "aceObjectId`, `aceObjectDescriptionFlags`, " +
                                "`weenieClassId`, `weenieHeaderFlags`, " +
                                "`weenieHeaderFlags2`, " +
                                "`currentMotionState`, " +
                                "`physicsDescriptionFlag`" +
                                ")" + Environment.NewLine + "VALUES (" +

                            $"{parsed.wdesc._wcid}, {parsed.wdesc._bitfield}, " +
                            $"{parsed.wdesc._wcid}, {parsed.wdesc.header}, "; //+


                            if ((parsed.wdesc._bitfield & (uint)PublicWeenieDesc.BitfieldIndex.BF_INCLUDES_SECOND_HEADER) != 0)
                                line += $"{parsed.wdesc.header2}, "; //+
                            else
                                line += $"NULL, "; //+

                            didsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeDID.ICON_DID}, {(uint)parsed.wdesc._iconID}) /* {Enum.GetName(typeof(STypeDID), STypeDID.ICON_DID)} */" + Environment.NewLine;
                            if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_IconOverlay) != 0)
                                didsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeDID.ICON_OVERLAY_DID}, {(uint)parsed.wdesc._iconOverlayID}) /* {Enum.GetName(typeof(STypeDID), STypeDID.ICON_OVERLAY_DID)} */" + Environment.NewLine;
                            if ((parsed.wdesc.header2 & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader2.PWD2_Packed_IconUnderlay) != 0)
                                didsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeDID.ICON_UNDERLAY_DID}, {(uint)parsed.wdesc._iconUnderlayID}) /* {Enum.GetName(typeof(STypeDID), STypeDID.ICON_UNDERLAY_DID)} */" + Environment.NewLine;

                            if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.CSetup) != 0)
                                didsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeDID.SETUP_DID}, {(uint)parsed.physicsdesc.setup_id}) /* {Enum.GetName(typeof(STypeDID), STypeDID.SETUP_DID)} */" + Environment.NewLine;
                            if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.STABLE) != 0)
                                didsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeDID.SOUND_TABLE_DID}, {(uint)parsed.physicsdesc.stable_id}) /* {Enum.GetName(typeof(STypeDID), STypeDID.SOUND_TABLE_DID)} */" + Environment.NewLine;
                            if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.MTABLE) != 0)
                                didsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeDID.MOTION_TABLE_DID}, {(uint)parsed.physicsdesc.mtable_id}) /* {Enum.GetName(typeof(STypeDID), STypeDID.MOTION_TABLE_DID)} */" + Environment.NewLine;
                            if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.MOVEMENT) != 0)
                                line += $"'{ConvertMovementBufferToString(parsed.physicsdesc.movement_buffer)}', ";
                            else
                                line += $"NULL, ";
                            if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.ANIMFRAME_ID) != 0)
                                intsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeInt.PLACEMENT_POSITION_INT}, {(uint)parsed.physicsdesc.animframe_id}) /* {Enum.GetName(typeof(STypeInt), STypeInt.PLACEMENT_POSITION_INT)} */" + Environment.NewLine;
                            if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.PETABLE) != 0)
                                didsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeDID.PHYSICS_EFFECT_TABLE_DID}, {(uint)parsed.physicsdesc.phstable_id}) /* {Enum.GetName(typeof(STypeDID), STypeDID.PHYSICS_EFFECT_TABLE_DID)} */" + Environment.NewLine;
                            line += $"{parsed.physicsdesc.bitfield}";
                            if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_SpellID) != 0)
                                didsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeDID.SPELL_DID}, {(uint)parsed.wdesc._spellID}) /* {Enum.GetName(typeof(STypeDID), STypeDID.SPELL_DID)} - {Enum.GetName(typeof(SpellID), parsed.wdesc._spellID)} */" + Environment.NewLine;
                            if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_PScript) != 0)
                                didsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeDID.PHYSICS_SCRIPT_DID}, {(uint)parsed.wdesc._pscript}) /* {Enum.GetName(typeof(STypeDID), STypeDID.PHYSICS_SCRIPT_DID)} */" + Environment.NewLine;
                            if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.DEFAULT_SCRIPT) != 0)
                                didsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeDID.ACTIVATION_ANIMATION_DID}, {(uint)parsed.physicsdesc.default_script}) /* {Enum.GetName(typeof(STypeDID), STypeDID.ACTIVATION_ANIMATION_DID)} */" + Environment.NewLine;

                            line += ");" + Environment.NewLine;

                            writer.WriteLine(line);

                            strsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeString.NAME_STRING}, '{parsed.wdesc._name.m_buffer.Replace("'", "''")}') /* {Enum.GetName(typeof(STypeString), STypeString.NAME_STRING)} */" + Environment.NewLine;
                            intsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeInt.ITEM_TYPE_INT}, {(uint)parsed.wdesc._type}) /* {Enum.GetName(typeof(STypeInt), STypeInt.ITEM_TYPE_INT)} */" + Environment.NewLine;

                            if (parsed.objdesc.subpalettes.Count > 0)
                                //intsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeInt.PALETTE_TEMPLATE_INT}, {parsed.objdesc.paletteID})" + Environment.NewLine;
                                didsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeDID.PALETTE_BASE_DID}, {(uint)parsed.objdesc.paletteID}) /* {Enum.GetName(typeof(STypeDID), STypeDID.PALETTE_BASE_DID)} */" + Environment.NewLine;

                            if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_AmmoType) != 0)
                                intsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeInt.AMMO_TYPE_INT}, {(uint)parsed.wdesc._ammoType}) /* {Enum.GetName(typeof(STypeInt), STypeInt.AMMO_TYPE_INT)} */" + Environment.NewLine;
                            if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_BlipColor) != 0)
                                intsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeInt.RADARBLIP_COLOR_INT}, {parsed.wdesc._blipColor}) /* {Enum.GetName(typeof(STypeInt), STypeInt.RADARBLIP_COLOR_INT)} */" + Environment.NewLine;
                            if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_Burden) != 0)
                                intsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeInt.ENCUMB_VAL_INT}, {parsed.wdesc._burden}) /* {Enum.GetName(typeof(STypeInt), STypeInt.ENCUMB_VAL_INT)} */" + Environment.NewLine;
                            if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_CombatUse) != 0)
                                intsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeInt.COMBAT_USE_INT}, {parsed.wdesc._combatUse}) /* {Enum.GetName(typeof(STypeInt), STypeInt.COMBAT_USE_INT)} */" + Environment.NewLine;
                            if ((parsed.wdesc.header2 & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader2.PWD2_Packed_CooldownDuration) != 0)
                                floatsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeFloat.COOLDOWN_DURATION_FLOAT}, {parsed.wdesc._cooldown_duration}) /* {Enum.GetName(typeof(STypeFloat), STypeFloat.COOLDOWN_DURATION_FLOAT)} */" + Environment.NewLine;
                            if ((parsed.wdesc.header2 & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader2.PWD2_Packed_CooldownID) != 0)
                                intsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeInt.SHARED_COOLDOWN_INT}, {parsed.wdesc._cooldown_id}) /* {Enum.GetName(typeof(STypeInt), STypeInt.SHARED_COOLDOWN_INT)} */" + Environment.NewLine;
                            if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_UIEffects) != 0)
                                intsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeInt.UI_EFFECTS_INT}, {parsed.wdesc._effects}) /* {Enum.GetName(typeof(STypeInt), STypeInt.UI_EFFECTS_INT)} */" + Environment.NewLine;
                            if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_ContainersCapacity) != 0)
                                intsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeInt.CONTAINERS_CAPACITY_INT}, {parsed.wdesc._containersCapacity}) /* {Enum.GetName(typeof(STypeInt), STypeInt.CONTAINERS_CAPACITY_INT)} */" + Environment.NewLine;
                            if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_HookType) != 0)
                                intsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeInt.HOOK_TYPE_INT}, {(uint)parsed.wdesc._hook_type}) /* {Enum.GetName(typeof(STypeInt), STypeInt.HOOK_TYPE_INT)} */" + Environment.NewLine;
                            if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_HookItemTypes) != 0)
                                intsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeInt.HOOK_ITEM_TYPE_INT}, {parsed.wdesc._hook_item_types}) /* {Enum.GetName(typeof(STypeInt), STypeInt.HOOK_ITEM_TYPE_INT)} */" + Environment.NewLine;
                            if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_ItemsCapacity) != 0)
                                intsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeInt.ITEMS_CAPACITY_INT}, {parsed.wdesc._itemsCapacity}) /* {Enum.GetName(typeof(STypeInt), STypeInt.ITEMS_CAPACITY_INT)} */" + Environment.NewLine;
                            //if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_Location) != 0)
                            //    intsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeInt.CURRENT_WIELDED_LOCATION_INT}, {parsed.wdesc._location})" + Environment.NewLine;
                            if ((parsed.wdesc.header & unchecked((uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_MaterialType)) != 0)
                                intsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeInt.MATERIAL_TYPE_INT}, {(uint)parsed.wdesc._material_type}) /* {Enum.GetName(typeof(STypeInt), STypeInt.MATERIAL_TYPE_INT)} */" + Environment.NewLine;
                            if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_MaxStackSize) != 0)
                                intsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeInt.MAX_STACK_SIZE_INT}, {parsed.wdesc._maxStackSize}) /* {Enum.GetName(typeof(STypeInt), STypeInt.MAX_STACK_SIZE_INT)} */" + Environment.NewLine;
                            if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_MaxStructure) != 0)
                                intsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeInt.MAX_STRUCTURE_INT}, {parsed.wdesc._maxStructure}) /* {Enum.GetName(typeof(STypeInt), STypeInt.MAX_STRUCTURE_INT)} */" + Environment.NewLine;
                            if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_RadarEnum) != 0)
                                intsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeInt.SHOWABLE_ON_RADAR_INT}, {(uint)parsed.wdesc._radar_enum}) /* {Enum.GetName(typeof(STypeInt), STypeInt.SHOWABLE_ON_RADAR_INT)} */" + Environment.NewLine;
                            //if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_StackSize) != 0)
                            //    intsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeInt.STACK_SIZE_INT}, {parsed.wdesc._stackSize})" + Environment.NewLine;
                            if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_StackSize) != 0)
                                intsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeInt.STACK_SIZE_INT}, {parsed.wdesc._stackSize}) /* {Enum.GetName(typeof(STypeInt), STypeInt.STACK_SIZE_INT)} */" + Environment.NewLine;
                            //if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_Structure) != 0)
                            //    intsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeInt.STRUCTURE_INT}, {parsed.wdesc._structure})" + Environment.NewLine;
                            if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_Structure) != 0)
                                intsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeInt.STRUCTURE_INT}, {parsed.wdesc._maxStructure}) /* {Enum.GetName(typeof(STypeInt), STypeInt.STRUCTURE_INT)} */" + Environment.NewLine;
                            if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_TargetType) != 0)
                                intsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeInt.TARGET_TYPE_INT}, {(uint)parsed.wdesc._targetType}) /* {Enum.GetName(typeof(STypeInt), STypeInt.TARGET_TYPE_INT)} */" + Environment.NewLine;
                            if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_Useability) != 0)
                                intsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeInt.ITEM_USEABLE_INT}, {(uint)parsed.wdesc._useability}) /* {Enum.GetName(typeof(STypeInt), STypeInt.ITEM_USEABLE_INT)} */" + Environment.NewLine;
                            if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_UseRadius) != 0)
                                floatsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeFloat.USE_RADIUS_FLOAT}, {parsed.wdesc._useRadius}) /* {Enum.GetName(typeof(STypeFloat), STypeFloat.USE_RADIUS_FLOAT)} */" + Environment.NewLine;
                            if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_ValidLocations) != 0)
                                intsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeInt.LOCATIONS_INT}, {parsed.wdesc._valid_locations}) /* {Enum.GetName(typeof(STypeInt), STypeInt.LOCATIONS_INT)} */" + Environment.NewLine;
                            if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_Value) != 0)
                                intsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeInt.VALUE_INT}, {parsed.wdesc._value}) /* {Enum.GetName(typeof(STypeInt), STypeInt.VALUE_INT)} */" + Environment.NewLine;
                            //if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_ContainerID) != 0)
                            //{
                            //    line = $"{sqlCommand} INTO `ace_object_properties_int` (`aceObjectId`, `intPropertyId`, `propertyValue`)" + Environment.NewLine;
                            //    line += $"VALUES ({parsed.object_id}, {(uint)STypeInt.???}, {parsed.wdesc._containerID})" + Environment.NewLine;
                            //    line = line.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                            //    writer.WriteLine(line);
                            //}
                            //if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_WielderID) != 0)
                            //{
                            //    line = $"{sqlCommand} INTO `ace_object_properties_int` (`aceObjectId`, `intPropertyId`, `propertyValue`)" + Environment.NewLine;
                            //    line += $"VALUES ({parsed.object_id}, {(uint)STypeInt.???}, {parsed.wdesc._wielderID})" + Environment.NewLine;
                            //    line = line.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                            //    writer.WriteLine(line);
                            //}
                            //if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_HouseOwner) != 0)
                            //{
                            //    line = $"{sqlCommand} INTO `ace_object_properties_int` (`aceObjectId`, `intPropertyId`, `propertyValue`)" + Environment.NewLine;
                            //    line += $"VALUES ({parsed.object_id}, {(uint)STypeInt.???}, {parsed.wdesc._house_owner_iid})" + Environment.NewLine;
                            //    line = line.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                            //    writer.WriteLine(line);
                            //}
                            //if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_HouseRestrictions) != 0)
                            //{
                            //    line = $"{sqlCommand} INTO `ace_object_properties_int` (`aceObjectId`, `intPropertyId`, `propertyValue`)" + Environment.NewLine;
                            //    line += $"VALUES ({parsed.object_id}, {(uint)STypeInt.???}, {parsed.wdesc.???})" + Environment.NewLine;
                            //    line = line.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                            //    writer.WriteLine(line);
                            //}
                            //if ((parsed.wdesc.header2 & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader2.PWD2_Packed_PetOwner) != 0)
                            //{
                            //    line = $"{sqlCommand} INTO `ace_object_properties_int` (`aceObjectId`, `intPropertyId`, `propertyValue`)" + Environment.NewLine;
                            //    line += $"VALUES ({parsed.object_id}, {(uint)STypeInt.???}, {parsed.wdesc._pet_owner})" + Environment.NewLine;
                            //    line = line.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                            //    writer.WriteLine(line);
                            //}
                            //if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_Monarch) != 0)
                            //{
                            //    line = $"{sqlCommand} INTO `ace_object_properties_int` (`aceObjectId`, `intPropertyId`, `propertyValue`)" + Environment.NewLine;
                            //    line += $"VALUES ({parsed.object_id}, {(uint)STypeInt.???}, {parsed.wdesc._monarch})" + Environment.NewLine;
                            //    line = line.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                            //    writer.WriteLine(line);
                            //}
                            //if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_Workmanship) != 0)
                            //{
                            //    line = $"{sqlCommand} INTO `ace_object_properties_int` (`aceObjectId`, `intPropertyId`, `propertyValue`)" + Environment.NewLine;
                            //    line += $"VALUES ({parsed.object_id}, {(uint)STypeInt.ITEM_WORKMANSHIP_INT}, {parsed.wdesc._workmanship})" + Environment.NewLine;
                            //    line = line.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                            //    writer.WriteLine(line);
                            //}
                            if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_PluralName) != 0)
                                strsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeString.PLURAL_NAME_STRING}, '{parsed.wdesc._plural_name.m_buffer.Replace("'", "''")}') /* {Enum.GetName(typeof(STypeString), STypeString.PLURAL_NAME_STRING)} */" + Environment.NewLine;
                            if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_Priority) != 0)
                                intsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeInt.CLOTHING_PRIORITY_INT}, {parsed.wdesc._priority}) /* {Enum.GetName(typeof(STypeInt), STypeInt.CLOTHING_PRIORITY_INT)} */" + Environment.NewLine;
                            if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.DEFAULT_SCRIPT_INTENSITY) != 0)
                                floatsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeFloat.PHYSICS_SCRIPT_INTENSITY_FLOAT}, {parsed.physicsdesc.default_script_intensity}) /* {Enum.GetName(typeof(STypeFloat), STypeFloat.PHYSICS_SCRIPT_INTENSITY_FLOAT)} */" + Environment.NewLine;
                            if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.ELASTICITY) != 0)
                                floatsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeFloat.ELASTICITY_FLOAT}, {parsed.physicsdesc.elasticity}) /* {Enum.GetName(typeof(STypeFloat), STypeFloat.ELASTICITY_FLOAT)} */" + Environment.NewLine;
                            if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.FRICTION) != 0)
                                floatsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeFloat.FRICTION_FLOAT}, {parsed.physicsdesc.friction}) /* {Enum.GetName(typeof(STypeFloat), STypeFloat.FRICTION_FLOAT)} */" + Environment.NewLine;
                            if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.PARENT) != 0)
                            {
                                //line = $"{sqlCommand} INTO `ace_object_properties_int` (`aceObjectId`, `intPropertyId`, `propertyValue`)" + Environment.NewLine;
                                //line += $"VALUES ({parsed.object_id}, {STypeInt.???}, {parsed.physicsdesc.parent_id})" + Environment.NewLine;
                                //line = line.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                //writer.WriteLine(line);

                                //line = $"{sqlCommand} INTO `ace_object_properties_int` (`aceObjectId`, `intPropertyId`, `propertyValue`)" + Environment.NewLine;
                                intsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeInt.PARENT_LOCATION_INT}, {parsed.physicsdesc.location_id}) /* {Enum.GetName(typeof(STypeInt), STypeInt.PARENT_LOCATION_INT)} */" + Environment.NewLine;
                                //line = line.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                //writer.WriteLine(line);
                            }
                            if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.OBJSCALE) != 0)
                                floatsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeFloat.DEFAULT_SCALE_FLOAT}, {parsed.physicsdesc.object_scale}) /* {Enum.GetName(typeof(STypeFloat), STypeFloat.DEFAULT_SCALE_FLOAT)} */" + Environment.NewLine;

                            intsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeInt.PHYSICS_STATE_INT}, {(uint)parsed.physicsdesc.state}) /* {Enum.GetName(typeof(STypeInt), STypeInt.PHYSICS_STATE_INT)} */" + Environment.NewLine;
                            if (((uint)parsed.physicsdesc.state & (uint)PhysicsState.STATIC_PS) != 0)
                                boolsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeBool.STUCK_BOOL}, {true}) /* {Enum.GetName(typeof(STypeBool), STypeBool.STUCK_BOOL)} */" + Environment.NewLine;
                            if (((uint)parsed.physicsdesc.state & (uint)PhysicsState.ETHEREAL_PS) != 0)
                                boolsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeBool.ETHEREAL_BOOL}, {true}) /* {Enum.GetName(typeof(STypeBool), STypeBool.ETHEREAL_BOOL)} */" + Environment.NewLine;
                            if (((uint)parsed.physicsdesc.state & (uint)PhysicsState.REPORT_COLLISIONS_PS) != 0)
                                boolsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeBool.REPORT_COLLISIONS_BOOL}, {true}) /* {Enum.GetName(typeof(STypeBool), STypeBool.REPORT_COLLISIONS_BOOL)} */" + Environment.NewLine;
                            if (((uint)parsed.physicsdesc.state & (uint)PhysicsState.IGNORE_COLLISIONS_PS) != 0)
                                boolsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeBool.IGNORE_COLLISIONS_BOOL}, {true}) /* {Enum.GetName(typeof(STypeBool), STypeBool.IGNORE_COLLISIONS_BOOL)} */" + Environment.NewLine;
                            if (((uint)parsed.physicsdesc.state & (uint)PhysicsState.NODRAW_PS) != 0)
                                boolsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeBool.NODRAW_BOOL}, {true}) /* {Enum.GetName(typeof(STypeBool), STypeBool.NODRAW_BOOL)} */" + Environment.NewLine;
                            if (((uint)parsed.physicsdesc.state & (uint)PhysicsState.GRAVITY_PS) != 0)
                                boolsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeBool.GRAVITY_STATUS_BOOL}, {true}) /* {Enum.GetName(typeof(STypeBool), STypeBool.GRAVITY_STATUS_BOOL)} */" + Environment.NewLine;
                            if (((uint)parsed.physicsdesc.state & (uint)PhysicsState.LIGHTING_ON_PS) != 0)
                                boolsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeBool.LIGHTS_STATUS_BOOL}, {true}) /* {Enum.GetName(typeof(STypeBool), STypeBool.LIGHTS_STATUS_BOOL)} */" + Environment.NewLine;
                            if (((uint)parsed.physicsdesc.state & (uint)PhysicsState.HIDDEN_PS) != 0)
                                boolsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeBool.VISIBILITY_BOOL}, {true}) /* {Enum.GetName(typeof(STypeBool), STypeBool.VISIBILITY_BOOL)} */" + Environment.NewLine;
                            if (((uint)parsed.physicsdesc.state & (uint)PhysicsState.SCRIPTED_COLLISION_PS) != 0)
                                boolsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeBool.SCRIPTED_COLLISION_BOOL}, {true}) /* {Enum.GetName(typeof(STypeBool), STypeBool.SCRIPTED_COLLISION_BOOL)} */" + Environment.NewLine;
                            if (((uint)parsed.physicsdesc.state & (uint)PhysicsState.INELASTIC_PS) != 0)
                                boolsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeBool.INELASTIC_BOOL}, {true}) /* {Enum.GetName(typeof(STypeBool), STypeBool.INELASTIC_BOOL)} */" + Environment.NewLine;
                            if (((uint)parsed.physicsdesc.state & (uint)PhysicsState.CLOAKED_PS) != 0)
                                boolsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeBool.HIDDEN_ADMIN_BOOL}, {true}) /* {Enum.GetName(typeof(STypeBool), STypeBool.HIDDEN_ADMIN_BOOL)} */" + Environment.NewLine;
                            if (((uint)parsed.physicsdesc.state & (uint)PhysicsState.REPORT_COLLISIONS_AS_ENVIRONMENT_PS) != 0)
                                boolsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeBool.REPORT_COLLISIONS_AS_ENVIRONMENT_BOOL}, {true}) /* {Enum.GetName(typeof(STypeBool), STypeBool.REPORT_COLLISIONS_AS_ENVIRONMENT_BOOL)} */" + Environment.NewLine;
                            if (((uint)parsed.physicsdesc.state & (uint)PhysicsState.EDGE_SLIDE_PS) != 0)
                                boolsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeBool.ALLOW_EDGE_SLIDE_BOOL}, {true}) /* {Enum.GetName(typeof(STypeBool), STypeBool.ALLOW_EDGE_SLIDE_BOOL)} */" + Environment.NewLine;
                            if (((uint)parsed.physicsdesc.state & (uint)PhysicsState.FROZEN_PS) != 0)
                                boolsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeBool.IS_FROZEN_BOOL}, {true}) /* {Enum.GetName(typeof(STypeBool), STypeBool.IS_FROZEN_BOOL)} */" + Environment.NewLine;


                            if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.TRANSLUCENCY) != 0)
                                floatsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeFloat.TRANSLUCENCY_FLOAT}, {parsed.physicsdesc.translucency}) /* {Enum.GetName(typeof(STypeFloat), STypeFloat.TRANSLUCENCY_FLOAT)} */" + Environment.NewLine;
                            //if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.VELOCITY) != 0)
                            //{
                            //    line = $"{sqlCommand} INTO `ace_object_properties_double` (`aceObjectId`, `dblPropertyId`, `propertyValue`)" + Environment.NewLine;
                            //    line += $"VALUES ({parsed.object_id}, {(uint)STypeFloat.MAXIMUM_VELOCITY_FLOAT}, {parsed.physicsdesc.velocity})" + Environment.NewLine;
                            //    line = line.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                            //    writer.WriteLine(line);
                            //}

                            if (((uint)parsed.wdesc._bitfield & (uint)PublicWeenieDesc.BitfieldIndex.BF_ATTACKABLE) != 0)
                                boolsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeBool.ATTACKABLE_BOOL}, {true}) /* {Enum.GetName(typeof(STypeBool), STypeBool.ATTACKABLE_BOOL)} */" + Environment.NewLine;
                            if (((uint)parsed.wdesc._bitfield & (uint)PublicWeenieDesc.BitfieldIndex.BF_HIDDEN_ADMIN) != 0)
                                boolsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeBool.HIDDEN_ADMIN_BOOL}, {true}) /* {Enum.GetName(typeof(STypeBool), STypeBool.HIDDEN_ADMIN_BOOL)} */" + Environment.NewLine;
                            if (((uint)parsed.wdesc._bitfield & (uint)PublicWeenieDesc.BitfieldIndex.BF_IMMUNE_CELL_RESTRICTIONS) != 0)
                                boolsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeBool.IGNORE_HOUSE_BARRIERS_BOOL}, {true}) /* {Enum.GetName(typeof(STypeBool), STypeBool.IGNORE_HOUSE_BARRIERS_BOOL)} */" + Environment.NewLine;
                            if (((uint)parsed.wdesc._bitfield & (uint)PublicWeenieDesc.BitfieldIndex.BF_INSCRIBABLE) != 0)
                                boolsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeBool.INSCRIBABLE_BOOL}, {true}) /* {Enum.GetName(typeof(STypeBool), STypeBool.INSCRIBABLE_BOOL)} */" + Environment.NewLine;
                            //if (((uint)parsed.wdesc._bitfield & (uint)PublicWeenieDesc.BitfieldIndex.BF_PLAYER_KILLER) != 0)
                            //    boolsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeBool.PK_KILLER_BOOL}, {true})" + Environment.NewLine;
                            if (((uint)parsed.wdesc._bitfield & (uint)PublicWeenieDesc.BitfieldIndex.BF_REQUIRES_PACKSLOT) != 0)
                                boolsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeBool.REQUIRES_BACKPACK_SLOT_BOOL}, {true}) /* {Enum.GetName(typeof(STypeBool), STypeBool.REQUIRES_BACKPACK_SLOT_BOOL)} */" + Environment.NewLine;
                            if (((uint)parsed.wdesc._bitfield & (uint)PublicWeenieDesc.BitfieldIndex.BF_RETAINED) != 0)
                                boolsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeBool.RETAINED_BOOL}, {true}) /* {Enum.GetName(typeof(STypeBool), STypeBool.RETAINED_BOOL)} */" + Environment.NewLine;
                            if (((uint)parsed.wdesc._bitfield & (uint)PublicWeenieDesc.BitfieldIndex.BF_STUCK) != 0)
                                boolsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeBool.STUCK_BOOL}, {true}) /* {Enum.GetName(typeof(STypeBool), STypeBool.STUCK_BOOL)} */" + Environment.NewLine;
                            if (((uint)parsed.wdesc._bitfield & (uint)PublicWeenieDesc.BitfieldIndex.BF_UI_HIDDEN) != 0)
                                boolsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeBool.UI_HIDDEN_BOOL}, {true}) /* {Enum.GetName(typeof(STypeBool), STypeBool.UI_HIDDEN_BOOL)} */" + Environment.NewLine;
                            //if (((uint)parsed.wdesc._bitfield & (uint)PublicWeenieDesc.BitfieldIndex.BF_VENDOR) != 0)
                            //    boolsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeBool.VENDOR_SERVICE_BOOL}, {true})" + Environment.NewLine;
                            if (((uint)parsed.wdesc._bitfield & (uint)PublicWeenieDesc.BitfieldIndex.BF_WIELD_LEFT) != 0)
                                boolsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeBool.AUTOWIELD_LEFT_BOOL}, {true}) /* {Enum.GetName(typeof(STypeBool), STypeBool.AUTOWIELD_LEFT_BOOL)} */" + Environment.NewLine;
                            if (((uint)parsed.wdesc._bitfield & (uint)PublicWeenieDesc.BitfieldIndex.BF_WIELD_ON_USE) != 0)
                                boolsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeBool.WIELD_ON_USE_BOOL}, {true}) /* {Enum.GetName(typeof(STypeBool), STypeBool.WIELD_ON_USE_BOOL)} */" + Environment.NewLine;
                            //if (((uint)parsed.wdesc._bitfield & (uint)PublicWeenieDesc.BitfieldIndex.BF_ADMIN) != 0)
                            //    boolsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeBool.IS_ADMIN_BOOL}, {true})" + Environment.NewLine;

                            if (weeniesWeenieType.ContainsKey(parsed.wdesc._wcid))
                            {
                                uint weenieType = 0;
                                weeniesWeenieType.TryGetValue(parsed.wdesc._wcid, out weenieType);
                                intsLine += $"     , ({parsed.wdesc._wcid}, {(uint)9007}, {weenieType}) /* {Enum.GetName(typeof(WeenieType), weenieType)} */" + Environment.NewLine;
                            }

                            if (strsLine != "")
                            {
                                strsLine = $"{sqlCommand} INTO `ace_object_properties_string` (`aceObjectId`, `strPropertyId`, `propertyValue`)" + Environment.NewLine
                                    + "VALUES " + strsLine.TrimStart("     ,".ToCharArray());
                                strsLine = strsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                writer.WriteLine(strsLine);
                            }
                            if (didsLine != "")
                            {
                                didsLine = $"{sqlCommand} INTO `ace_object_properties_did` (`aceObjectId`, `didPropertyId`, `propertyValue`)" + Environment.NewLine
                                    + "VALUES " + didsLine.TrimStart("     ,".ToCharArray());
                                didsLine = didsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                writer.WriteLine(didsLine);
                            }
                            if (iidsLine != "")
                            {
                                iidsLine = $"{sqlCommand} INTO `ace_object_properties_iid` (`aceObjectId`, `iidPropertyId`, `propertyValue`)" + Environment.NewLine
                                    + "VALUES " + iidsLine.TrimStart("     ,".ToCharArray());
                                iidsLine = iidsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                writer.WriteLine(iidsLine);
                            }
                            if (intsLine != "")
                            {
                                intsLine = $"{sqlCommand} INTO `ace_object_properties_int` (`aceObjectId`, `intPropertyId`, `propertyValue`)" + Environment.NewLine
                                    + "VALUES " + intsLine.TrimStart("     ,".ToCharArray());
                                intsLine = intsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                writer.WriteLine(intsLine);
                            }
                            if (bigintsLine != "")
                            {
                                bigintsLine = $"{sqlCommand} INTO `ace_object_properties_bigint` (`aceObjectId`, `bigIntPropertyId`, `propertyValue`)" + Environment.NewLine
                                    + "VALUES " + bigintsLine.TrimStart("     ,".ToCharArray());
                                bigintsLine = bigintsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                writer.WriteLine(bigintsLine);
                            }
                            if (floatsLine != "")
                            {
                                floatsLine = $"{sqlCommand} INTO `ace_object_properties_double` (`aceObjectId`, `dblPropertyId`, `propertyValue`)" + Environment.NewLine
                                    + "VALUES " + floatsLine.TrimStart("     ,".ToCharArray());
                                floatsLine = floatsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                writer.WriteLine(floatsLine);
                            }
                            if (boolsLine != "")
                            {
                                boolsLine = $"{sqlCommand} INTO `ace_object_properties_bool` (`aceObjectId`, `boolPropertyId`, `propertyValue`)" + Environment.NewLine
                                    + "VALUES " + boolsLine.TrimStart("     ,".ToCharArray());
                                boolsLine = boolsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                writer.WriteLine(boolsLine);
                            }


                            once = false;
                            if (parsed.objdesc.subpalettes.Count > 0)
                            {
                                line = $"{sqlCommand} INTO `ace_object_palette_change` (`aceObjectId`, `subPaletteId`, `offset`, `length`)" + Environment.NewLine;

                                foreach (var subPalette in parsed.objdesc.subpalettes)
                                {
                                    if (once)
                                    {
                                        line += $"     , ({parsed.wdesc._wcid}, {subPalette.subID}, {subPalette.offset}, {subPalette.numcolors})" + Environment.NewLine;
                                    }
                                    else
                                    {
                                        line += $"VALUES ({parsed.wdesc._wcid}, {subPalette.subID}, {subPalette.offset}, {subPalette.numcolors})" + Environment.NewLine;
                                        once = true;
                                    }
                                }

                                line = line.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                writer.WriteLine(line);
                            }

                            once = false;
                            if (parsed.objdesc.tmChanges.Count > 0)
                            {
                                line = $"{sqlCommand} INTO `ace_object_texture_map_change` (`aceObjectId`, `index`, `oldId`, `newId`)" + Environment.NewLine;

                                foreach (var texture in parsed.objdesc.tmChanges)
                                {
                                    if (once)
                                    {
                                        line += $"     , ({parsed.wdesc._wcid}, {texture.part_index}, {texture.old_tex_id}, {texture.new_tex_id})" + Environment.NewLine;
                                    }
                                    else
                                    {
                                        line += $"VALUES ({parsed.wdesc._wcid}, {texture.part_index}, {texture.old_tex_id}, {texture.new_tex_id})" + Environment.NewLine;
                                        once = true;
                                    }
                                }

                                line = line.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                writer.WriteLine(line);
                            }

                            once = false;
                            if (parsed.objdesc.apChanges.Count > 0)
                            {
                                line = $"{sqlCommand} INTO `ace_object_animation_change` (`aceObjectId`, `index`, `animationId`)" + Environment.NewLine;

                                foreach (var animation in parsed.objdesc.apChanges)
                                {
                                    if (once)
                                    {
                                        line += $"     , ({parsed.wdesc._wcid}, {animation.part_index}, {animation.part_id})" + Environment.NewLine;
                                    }
                                    else
                                    {
                                        line += $"VALUES ({parsed.wdesc._wcid}, {animation.part_index}, {animation.part_id})" + Environment.NewLine;
                                        once = true;
                                    }
                                }

                                line = line.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                writer.WriteLine(line);
                            }
                        }
                    }
                }
            }
        }

        private void WriteWeenieData(Dictionary<string, List<CM_Physics.CreateObject>> weenies, string outputFolder, Dictionary<uint, uint> weeniesWeenieType)
        {
            string templateFolder = Path.Combine(outputFolder, "1-weenies");

            string sqlCommand = "INSERT";
            //string sqlCommand = "REPLACE";

            if (!Directory.Exists(templateFolder))
                Directory.CreateDirectory(templateFolder);

            foreach (string key in weenies.Keys)
            {
                string filename = Path.Combine(templateFolder, $"{key}.sql");

                if (File.Exists(filename))
                    File.Delete(filename);

                foreach (var parsed in weenies[key])
                {
                    bool once = false;

                    //if (File.Exists(fullFile))
                    //{
                    //    FileInfo fi = new FileInfo(fullFile);

                    //    // go to the next file if it's bigger than a MB
                    //    if (fi.Length > ((1048576) * 40))
                    //    {
                    //        fileCount[parsed.wdesc._type]++;
                    //        fullFile = Path.Combine(staticFolder, $"{parsed.wdesc._type}_{fileCount[parsed.wdesc._type]}.sql");
                    //    }
                    //}
                                        
                    using (FileStream fs = new FileStream(filename, FileMode.Append))
                    {
                        using (StreamWriter writer = new StreamWriter(fs))
                        {
                            string weenieName = "";
                            if (Enum.IsDefined(typeof(WeenieClasses), (ushort)parsed.wdesc._wcid))
                            {
                                weenieName = Enum.GetName(typeof(WeenieClasses), parsed.wdesc._wcid).Substring(2);
                                //weenieName = weenieName.Substring(0, weenieName.Length - 6).Replace("_", "").ToLower();
                                weenieName = weenieName.Substring(0, weenieName.Length - 6).Replace("_", "-").ToLower();
                            }
                            else
                                weenieName = "ace" + parsed.wdesc._wcid.ToString() + "-" + parsed.wdesc._name.m_buffer.Replace("'", "").Replace(" ", "").Replace(".", "").Replace("(", "").Replace(")", "").Replace("+", "").Replace(":", "").Replace("_", "").Replace("-", "").Replace(",", "").ToLower();

                            string line = $"/* Weenie - {key} - {parsed.wdesc._name.m_buffer} ({parsed.wdesc._wcid}) */" + Environment.NewLine;

                            line += $"DELETE FROM ace_weenie_class WHERE weenieClassId = {parsed.wdesc._wcid};" + Environment.NewLine + Environment.NewLine;

                            line += $"{sqlCommand} INTO ace_weenie_class (`weenieClassId`, `weenieClassDescription`)" + Environment.NewLine +
                                   $"VALUES ({parsed.wdesc._wcid}, '{weenieName}');" + Environment.NewLine;
                            writer.WriteLine(line);

                            string intsLine = "", bigintsLine = "", floatsLine = "", boolsLine = "", strsLine = "", didsLine = "", iidsLine = "";

                            line = $"{sqlCommand} INTO `ace_object` (`" +
                                "aceObjectId`, `aceObjectDescriptionFlags`, " +
                                "`weenieClassId`, `weenieHeaderFlags`, " +
                                "`weenieHeaderFlags2`, " +
                                "`currentMotionState`, " +
                                "`physicsDescriptionFlag`" +
                                ")" + Environment.NewLine + "VALUES (" +

                            $"{parsed.wdesc._wcid}, {parsed.wdesc._bitfield}, " +
                            $"{parsed.wdesc._wcid}, {parsed.wdesc.header}, "; //+


                            if ((parsed.wdesc._bitfield & (uint)PublicWeenieDesc.BitfieldIndex.BF_INCLUDES_SECOND_HEADER) != 0)
                                line += $"{parsed.wdesc.header2}, "; //+
                            else
                                line += $"NULL, "; //+

                            didsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeDID.ICON_DID}, {(uint)parsed.wdesc._iconID})" + Environment.NewLine;
                            if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_IconOverlay) != 0)
                                didsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeDID.ICON_OVERLAY_DID}, {(uint)parsed.wdesc._iconOverlayID})" + Environment.NewLine;
                            if ((parsed.wdesc.header2 & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader2.PWD2_Packed_IconUnderlay) != 0)
                                didsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeDID.ICON_UNDERLAY_DID}, {(uint)parsed.wdesc._iconUnderlayID})" + Environment.NewLine;

                            if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.CSetup) != 0)
                                didsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeDID.SETUP_DID}, {(uint)parsed.physicsdesc.setup_id})" + Environment.NewLine;
                            if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.STABLE) != 0)
                                didsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeDID.SOUND_TABLE_DID}, {(uint)parsed.physicsdesc.stable_id})" + Environment.NewLine;
                            if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.MTABLE) != 0)
                                didsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeDID.MOTION_TABLE_DID}, {(uint)parsed.physicsdesc.mtable_id})" + Environment.NewLine;
                            if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.MOVEMENT) != 0)
                                line += $"'{ConvertMovementBufferToString(parsed.physicsdesc.movement_buffer)}', ";
                            else
                                line += $"NULL, ";
                            if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.ANIMFRAME_ID) != 0)
                                intsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeInt.PLACEMENT_POSITION_INT}, {(uint)parsed.physicsdesc.animframe_id})" + Environment.NewLine;
                            if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.PETABLE) != 0)
                                didsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeDID.PHYSICS_EFFECT_TABLE_DID}, {(uint)parsed.physicsdesc.phstable_id})" + Environment.NewLine;
                            line += $"{parsed.physicsdesc.bitfield}";
                            if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_SpellID) != 0)
                                didsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeDID.SPELL_DID}, {(uint)parsed.wdesc._spellID})" + Environment.NewLine;
                            if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_PScript) != 0)
                                didsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeDID.PHYSICS_SCRIPT_DID}, {(uint)parsed.wdesc._pscript})" + Environment.NewLine;
                            if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.DEFAULT_SCRIPT) != 0)
                                didsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeDID.USE_USER_ANIMATION_DID}, {(uint)parsed.physicsdesc.default_script})" + Environment.NewLine;

                            line += ");" + Environment.NewLine;

                            writer.WriteLine(line);

                            strsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeString.NAME_STRING}, '{parsed.wdesc._name.m_buffer.Replace("'", "''")}')" + Environment.NewLine;
                            intsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeInt.ITEM_TYPE_INT}, {(uint)parsed.wdesc._type})" + Environment.NewLine;

                            if (parsed.objdesc.subpalettes.Count > 0)
                                //intsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeInt.PALETTE_TEMPLATE_INT}, {parsed.objdesc.paletteID})" + Environment.NewLine;
                                didsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeDID.PALETTE_BASE_DID}, {(uint)parsed.objdesc.paletteID})" + Environment.NewLine;

                            if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_AmmoType) != 0)
                                intsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeInt.AMMO_TYPE_INT}, {(uint)parsed.wdesc._ammoType})" + Environment.NewLine;
                            if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_BlipColor) != 0)
                                intsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeInt.RADARBLIP_COLOR_INT}, {parsed.wdesc._blipColor})" + Environment.NewLine;
                            if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_Burden) != 0)
                                intsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeInt.ENCUMB_VAL_INT}, {parsed.wdesc._burden})" + Environment.NewLine;
                            if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_CombatUse) != 0)
                                intsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeInt.COMBAT_USE_INT}, {parsed.wdesc._combatUse})" + Environment.NewLine;
                            if ((parsed.wdesc.header2 & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader2.PWD2_Packed_CooldownDuration) != 0)
                                floatsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeFloat.COOLDOWN_DURATION_FLOAT}, {parsed.wdesc._cooldown_duration})" + Environment.NewLine;
                            if ((parsed.wdesc.header2 & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader2.PWD2_Packed_CooldownID) != 0)
                                intsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeInt.SHARED_COOLDOWN_INT}, {parsed.wdesc._cooldown_id})" + Environment.NewLine;
                            if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_UIEffects) != 0)
                                intsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeInt.UI_EFFECTS_INT}, {parsed.wdesc._effects})" + Environment.NewLine;
                            if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_ContainersCapacity) != 0)
                                intsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeInt.CONTAINERS_CAPACITY_INT}, {parsed.wdesc._containersCapacity})" + Environment.NewLine;
                            if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_HookType) != 0)
                                intsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeInt.HOOK_TYPE_INT}, {(uint)parsed.wdesc._hook_type})" + Environment.NewLine;
                            if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_HookItemTypes) != 0)
                                intsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeInt.HOOK_ITEM_TYPE_INT}, {parsed.wdesc._hook_item_types})" + Environment.NewLine;
                            if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_ItemsCapacity) != 0)
                                intsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeInt.ITEMS_CAPACITY_INT}, {parsed.wdesc._itemsCapacity})" + Environment.NewLine;
                            if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_Location) != 0)
                                intsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeInt.CURRENT_WIELDED_LOCATION_INT}, {parsed.wdesc._location})" + Environment.NewLine;
                            if ((parsed.wdesc.header & unchecked((uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_MaterialType)) != 0)
                                intsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeInt.MATERIAL_TYPE_INT}, {(uint)parsed.wdesc._material_type})" + Environment.NewLine;
                            if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_MaxStackSize) != 0)
                                intsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeInt.MAX_STACK_SIZE_INT}, {parsed.wdesc._maxStackSize})" + Environment.NewLine;
                            if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_MaxStructure) != 0)
                                intsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeInt.MAX_STRUCTURE_INT}, {parsed.wdesc._maxStructure})" + Environment.NewLine;
                            if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_RadarEnum) != 0)
                                intsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeInt.SHOWABLE_ON_RADAR_INT}, {(uint)parsed.wdesc._radar_enum})" + Environment.NewLine;
                            if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_StackSize) != 0)
                                intsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeInt.STACK_SIZE_INT}, {parsed.wdesc._stackSize})" + Environment.NewLine;
                            if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_Structure) != 0)
                                intsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeInt.STRUCTURE_INT}, {parsed.wdesc._structure})" + Environment.NewLine;
                            if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_TargetType) != 0)
                                intsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeInt.TARGET_TYPE_INT}, {(uint)parsed.wdesc._targetType})" + Environment.NewLine;
                            if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_Useability) != 0)
                                intsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeInt.ITEM_USEABLE_INT}, {(uint)parsed.wdesc._useability})" + Environment.NewLine;
                            if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_UseRadius) != 0)
                                floatsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeFloat.USE_RADIUS_FLOAT}, {parsed.wdesc._useRadius})" + Environment.NewLine;
                            if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_ValidLocations) != 0)
                                intsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeInt.LOCATIONS_INT}, {parsed.wdesc._valid_locations})" + Environment.NewLine;
                            if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_Value) != 0)
                                intsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeInt.VALUE_INT}, {parsed.wdesc._value})" + Environment.NewLine;
                            //if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_ContainerID) != 0)
                            //{
                            //    line = $"{sqlCommand} INTO `ace_object_properties_int` (`aceObjectId`, `intPropertyId`, `propertyValue`)" + Environment.NewLine;
                            //    line += $"VALUES ({parsed.object_id}, {(uint)STypeInt.???}, {parsed.wdesc._containerID})" + Environment.NewLine;
                            //    line = line.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                            //    writer.WriteLine(line);
                            //}
                            //if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_WielderID) != 0)
                            //{
                            //    line = $"{sqlCommand} INTO `ace_object_properties_int` (`aceObjectId`, `intPropertyId`, `propertyValue`)" + Environment.NewLine;
                            //    line += $"VALUES ({parsed.object_id}, {(uint)STypeInt.???}, {parsed.wdesc._wielderID})" + Environment.NewLine;
                            //    line = line.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                            //    writer.WriteLine(line);
                            //}
                            //if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_HouseOwner) != 0)
                            //{
                            //    line = $"{sqlCommand} INTO `ace_object_properties_int` (`aceObjectId`, `intPropertyId`, `propertyValue`)" + Environment.NewLine;
                            //    line += $"VALUES ({parsed.object_id}, {(uint)STypeInt.???}, {parsed.wdesc._house_owner_iid})" + Environment.NewLine;
                            //    line = line.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                            //    writer.WriteLine(line);
                            //}
                            //if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_HouseRestrictions) != 0)
                            //{
                            //    line = $"{sqlCommand} INTO `ace_object_properties_int` (`aceObjectId`, `intPropertyId`, `propertyValue`)" + Environment.NewLine;
                            //    line += $"VALUES ({parsed.object_id}, {(uint)STypeInt.???}, {parsed.wdesc.???})" + Environment.NewLine;
                            //    line = line.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                            //    writer.WriteLine(line);
                            //}
                            //if ((parsed.wdesc.header2 & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader2.PWD2_Packed_PetOwner) != 0)
                            //{
                            //    line = $"{sqlCommand} INTO `ace_object_properties_int` (`aceObjectId`, `intPropertyId`, `propertyValue`)" + Environment.NewLine;
                            //    line += $"VALUES ({parsed.object_id}, {(uint)STypeInt.???}, {parsed.wdesc._pet_owner})" + Environment.NewLine;
                            //    line = line.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                            //    writer.WriteLine(line);
                            //}
                            //if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_Monarch) != 0)
                            //{
                            //    line = $"{sqlCommand} INTO `ace_object_properties_int` (`aceObjectId`, `intPropertyId`, `propertyValue`)" + Environment.NewLine;
                            //    line += $"VALUES ({parsed.object_id}, {(uint)STypeInt.???}, {parsed.wdesc._monarch})" + Environment.NewLine;
                            //    line = line.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                            //    writer.WriteLine(line);
                            //}
                            //if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_Workmanship) != 0)
                            //{
                            //    line = $"{sqlCommand} INTO `ace_object_properties_int` (`aceObjectId`, `intPropertyId`, `propertyValue`)" + Environment.NewLine;
                            //    line += $"VALUES ({parsed.object_id}, {(uint)STypeInt.ITEM_WORKMANSHIP_INT}, {parsed.wdesc._workmanship})" + Environment.NewLine;
                            //    line = line.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                            //    writer.WriteLine(line);
                            //}
                            if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_PluralName) != 0)
                                strsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeString.PLURAL_NAME_STRING}, '{parsed.wdesc._plural_name.m_buffer.Replace("'", "''")}')" + Environment.NewLine;
                            if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_Priority) != 0)
                                intsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeInt.CLOTHING_PRIORITY_INT}, {parsed.wdesc._priority})" + Environment.NewLine;
                            if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.DEFAULT_SCRIPT_INTENSITY) != 0)
                                floatsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeFloat.PHYSICS_SCRIPT_INTENSITY_FLOAT}, {parsed.physicsdesc.default_script_intensity})" + Environment.NewLine;
                            if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.ELASTICITY) != 0)
                                floatsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeFloat.ELASTICITY_FLOAT}, {parsed.physicsdesc.elasticity})" + Environment.NewLine;
                            if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.FRICTION) != 0)
                                floatsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeFloat.FRICTION_FLOAT}, {parsed.physicsdesc.friction})" + Environment.NewLine;
                            if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.PARENT) != 0)
                            {
                                //line = $"{sqlCommand} INTO `ace_object_properties_int` (`aceObjectId`, `intPropertyId`, `propertyValue`)" + Environment.NewLine;
                                //line += $"VALUES ({parsed.object_id}, {STypeInt.???}, {parsed.physicsdesc.parent_id})" + Environment.NewLine;
                                //line = line.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                //writer.WriteLine(line);

                                //line = $"{sqlCommand} INTO `ace_object_properties_int` (`aceObjectId`, `intPropertyId`, `propertyValue`)" + Environment.NewLine;
                                intsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeInt.PARENT_LOCATION_INT}, {parsed.physicsdesc.location_id})" + Environment.NewLine;
                                //line = line.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                //writer.WriteLine(line);
                            }
                            if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.OBJSCALE) != 0)
                                floatsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeFloat.DEFAULT_SCALE_FLOAT}, {parsed.physicsdesc.object_scale})" + Environment.NewLine;

                            intsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeInt.PHYSICS_STATE_INT}, {(uint)parsed.physicsdesc.state})" + Environment.NewLine;
                            if (((uint)parsed.physicsdesc.state & (uint)PhysicsState.STATIC_PS) != 0)
                                boolsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeBool.STUCK_BOOL}, {true})" + Environment.NewLine;
                            if (((uint)parsed.physicsdesc.state & (uint)PhysicsState.ETHEREAL_PS) != 0)
                                boolsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeBool.ETHEREAL_BOOL}, {true})" + Environment.NewLine;
                            if (((uint)parsed.physicsdesc.state & (uint)PhysicsState.REPORT_COLLISIONS_PS) != 0)
                                boolsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeBool.REPORT_COLLISIONS_BOOL}, {true})" + Environment.NewLine;
                            if (((uint)parsed.physicsdesc.state & (uint)PhysicsState.IGNORE_COLLISIONS_PS) != 0)
                                boolsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeBool.IGNORE_COLLISIONS_BOOL}, {true})" + Environment.NewLine;
                            if (((uint)parsed.physicsdesc.state & (uint)PhysicsState.NODRAW_PS) != 0)
                                boolsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeBool.NODRAW_BOOL}, {true})" + Environment.NewLine;
                            if (((uint)parsed.physicsdesc.state & (uint)PhysicsState.GRAVITY_PS) != 0)
                                boolsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeBool.GRAVITY_STATUS_BOOL}, {true})" + Environment.NewLine;
                            if (((uint)parsed.physicsdesc.state & (uint)PhysicsState.LIGHTING_ON_PS) != 0)
                                boolsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeBool.LIGHTS_STATUS_BOOL}, {true})" + Environment.NewLine;
                            if (((uint)parsed.physicsdesc.state & (uint)PhysicsState.HIDDEN_PS) != 0)
                                boolsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeBool.VISIBILITY_BOOL}, {true})" + Environment.NewLine;
                            if (((uint)parsed.physicsdesc.state & (uint)PhysicsState.SCRIPTED_COLLISION_PS) != 0)
                                boolsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeBool.SCRIPTED_COLLISION_BOOL}, {true})" + Environment.NewLine;
                            if (((uint)parsed.physicsdesc.state & (uint)PhysicsState.INELASTIC_PS) != 0)
                                boolsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeBool.INELASTIC_BOOL}, {true})" + Environment.NewLine;
                            if (((uint)parsed.physicsdesc.state & (uint)PhysicsState.CLOAKED_PS) != 0)
                                boolsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeBool.HIDDEN_ADMIN_BOOL}, {true})" + Environment.NewLine;
                            if (((uint)parsed.physicsdesc.state & (uint)PhysicsState.REPORT_COLLISIONS_AS_ENVIRONMENT_PS) != 0)
                                boolsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeBool.REPORT_COLLISIONS_AS_ENVIRONMENT_BOOL}, {true})" + Environment.NewLine;
                            if (((uint)parsed.physicsdesc.state & (uint)PhysicsState.EDGE_SLIDE_PS) != 0)
                                boolsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeBool.ALLOW_EDGE_SLIDE_BOOL}, {true})" + Environment.NewLine;
                            if (((uint)parsed.physicsdesc.state & (uint)PhysicsState.FROZEN_PS) != 0)
                                boolsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeBool.IS_FROZEN_BOOL}, {true})" + Environment.NewLine;


                            if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.TRANSLUCENCY) != 0)
                                floatsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeFloat.TRANSLUCENCY_FLOAT}, {parsed.physicsdesc.translucency})" + Environment.NewLine;
                            //if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.VELOCITY) != 0)
                            //{
                            //    line = $"{sqlCommand} INTO `ace_object_properties_double` (`aceObjectId`, `dblPropertyId`, `propertyValue`)" + Environment.NewLine;
                            //    line += $"VALUES ({parsed.object_id}, {(uint)STypeFloat.MAXIMUM_VELOCITY_FLOAT}, {parsed.physicsdesc.velocity})" + Environment.NewLine;
                            //    line = line.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                            //    writer.WriteLine(line);
                            //}

                            if (((uint)parsed.wdesc._bitfield & (uint)PublicWeenieDesc.BitfieldIndex.BF_ATTACKABLE) != 0)
                                boolsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeBool.ATTACKABLE_BOOL}, {true})" + Environment.NewLine;
                            if (((uint)parsed.wdesc._bitfield & (uint)PublicWeenieDesc.BitfieldIndex.BF_HIDDEN_ADMIN) != 0)
                                boolsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeBool.HIDDEN_ADMIN_BOOL}, {true})" + Environment.NewLine;
                            if (((uint)parsed.wdesc._bitfield & (uint)PublicWeenieDesc.BitfieldIndex.BF_IMMUNE_CELL_RESTRICTIONS) != 0)
                                boolsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeBool.IGNORE_HOUSE_BARRIERS_BOOL}, {true})" + Environment.NewLine;
                            if (((uint)parsed.wdesc._bitfield & (uint)PublicWeenieDesc.BitfieldIndex.BF_INSCRIBABLE) != 0)
                                boolsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeBool.INSCRIBABLE_BOOL}, {true})" + Environment.NewLine;
                            //if (((uint)parsed.wdesc._bitfield & (uint)PublicWeenieDesc.BitfieldIndex.BF_PLAYER_KILLER) != 0)
                            //    boolsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeBool.PK_KILLER_BOOL}, {true})" + Environment.NewLine;
                            if (((uint)parsed.wdesc._bitfield & (uint)PublicWeenieDesc.BitfieldIndex.BF_REQUIRES_PACKSLOT) != 0)
                                boolsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeBool.REQUIRES_BACKPACK_SLOT_BOOL}, {true})" + Environment.NewLine;
                            if (((uint)parsed.wdesc._bitfield & (uint)PublicWeenieDesc.BitfieldIndex.BF_RETAINED) != 0)
                                boolsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeBool.RETAINED_BOOL}, {true})" + Environment.NewLine;
                            if (((uint)parsed.wdesc._bitfield & (uint)PublicWeenieDesc.BitfieldIndex.BF_STUCK) != 0)
                                boolsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeBool.STUCK_BOOL}, {true})" + Environment.NewLine;
                            if (((uint)parsed.wdesc._bitfield & (uint)PublicWeenieDesc.BitfieldIndex.BF_UI_HIDDEN) != 0)
                                boolsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeBool.UI_HIDDEN_BOOL}, {true})" + Environment.NewLine;
                            //if (((uint)parsed.wdesc._bitfield & (uint)PublicWeenieDesc.BitfieldIndex.BF_VENDOR) != 0)
                            //    boolsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeBool.VENDOR_SERVICE_BOOL}, {true})" + Environment.NewLine;
                            if (((uint)parsed.wdesc._bitfield & (uint)PublicWeenieDesc.BitfieldIndex.BF_WIELD_LEFT) != 0)
                                boolsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeBool.AUTOWIELD_LEFT_BOOL}, {true})" + Environment.NewLine;
                            if (((uint)parsed.wdesc._bitfield & (uint)PublicWeenieDesc.BitfieldIndex.BF_WIELD_ON_USE) != 0)
                                boolsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeBool.WIELD_ON_USE_BOOL}, {true})" + Environment.NewLine;
                            //if (((uint)parsed.wdesc._bitfield & (uint)PublicWeenieDesc.BitfieldIndex.BF_ADMIN) != 0)
                            //    boolsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeBool.IS_ADMIN_BOOL}, {true})" + Environment.NewLine;

                            if (weeniesWeenieType.ContainsKey(parsed.wdesc._wcid))
                            {
                                uint weenieType = 0;
                                weeniesWeenieType.TryGetValue(parsed.wdesc._wcid, out weenieType);
                                intsLine += $"     , ({parsed.wdesc._wcid}, {(uint)9007}, {weenieType})" + Environment.NewLine;
                            }

                            if (strsLine != "")
                            {
                                strsLine = $"{sqlCommand} INTO `ace_object_properties_string` (`aceObjectId`, `strPropertyId`, `propertyValue`)" + Environment.NewLine
                                    + "VALUES " + strsLine.TrimStart("     ,".ToCharArray());
                                strsLine = strsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                writer.WriteLine(strsLine);
                            }
                            if (didsLine != "")
                            {
                                didsLine = $"{sqlCommand} INTO `ace_object_properties_did` (`aceObjectId`, `didPropertyId`, `propertyValue`)" + Environment.NewLine
                                    + "VALUES " + didsLine.TrimStart("     ,".ToCharArray());
                                didsLine = didsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                writer.WriteLine(didsLine);
                            }
                            if (iidsLine != "")
                            {
                                iidsLine = $"{sqlCommand} INTO `ace_object_properties_iid` (`aceObjectId`, `iidPropertyId`, `propertyValue`)" + Environment.NewLine
                                    + "VALUES " + iidsLine.TrimStart("     ,".ToCharArray());
                                iidsLine = iidsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                writer.WriteLine(iidsLine);
                            }
                            if (intsLine != "")
                            {
                                intsLine = $"{sqlCommand} INTO `ace_object_properties_int` (`aceObjectId`, `intPropertyId`, `propertyValue`)" + Environment.NewLine
                                    + "VALUES " + intsLine.TrimStart("     ,".ToCharArray());
                                intsLine = intsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                writer.WriteLine(intsLine);
                            }
                            if (bigintsLine != "")
                            {
                                bigintsLine = $"{sqlCommand} INTO `ace_object_properties_bigint` (`aceObjectId`, `bigIntPropertyId`, `propertyValue`)" + Environment.NewLine
                                    + "VALUES " + bigintsLine.TrimStart("     ,".ToCharArray());
                                bigintsLine = bigintsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                writer.WriteLine(bigintsLine);
                            }
                            if (floatsLine != "")
                            {
                                floatsLine = $"{sqlCommand} INTO `ace_object_properties_double` (`aceObjectId`, `dblPropertyId`, `propertyValue`)" + Environment.NewLine
                                    + "VALUES " + floatsLine.TrimStart("     ,".ToCharArray());
                                floatsLine = floatsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                writer.WriteLine(floatsLine);
                            }
                            if (boolsLine != "")
                            {
                                boolsLine = $"{sqlCommand} INTO `ace_object_properties_bool` (`aceObjectId`, `boolPropertyId`, `propertyValue`)" + Environment.NewLine
                                    + "VALUES " + boolsLine.TrimStart("     ,".ToCharArray());
                                boolsLine = boolsLine.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                writer.WriteLine(boolsLine);
                            }


                            once = false;
                            if (parsed.objdesc.subpalettes.Count > 0)
                            {
                                line = $"{sqlCommand} INTO `ace_object_palette_change` (`aceObjectId`, `subPaletteId`, `offset`, `length`)" + Environment.NewLine;

                                foreach (var subPalette in parsed.objdesc.subpalettes)
                                {
                                    if (once)
                                    {
                                        line += $"     , ({parsed.wdesc._wcid}, {subPalette.subID}, {subPalette.offset}, {subPalette.numcolors})" + Environment.NewLine;
                                    }
                                    else
                                    {
                                        line += $"VALUES ({parsed.wdesc._wcid}, {subPalette.subID}, {subPalette.offset}, {subPalette.numcolors})" + Environment.NewLine;
                                        once = true;
                                    }
                                }

                                line = line.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                writer.WriteLine(line);
                            }

                            once = false;
                            if (parsed.objdesc.tmChanges.Count > 0)
                            {
                                line = $"{sqlCommand} INTO `ace_object_texture_map_change` (`aceObjectId`, `index`, `oldId`, `newId`)" + Environment.NewLine;

                                foreach (var texture in parsed.objdesc.tmChanges)
                                {
                                    if (once)
                                    {
                                        line += $"     , ({parsed.wdesc._wcid}, {texture.part_index}, {texture.old_tex_id}, {texture.new_tex_id})" + Environment.NewLine;
                                    }
                                    else
                                    {
                                        line += $"VALUES ({parsed.wdesc._wcid}, {texture.part_index}, {texture.old_tex_id}, {texture.new_tex_id})" + Environment.NewLine;
                                        once = true;
                                    }
                                }

                                line = line.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                writer.WriteLine(line);
                            }

                            once = false;
                            if (parsed.objdesc.apChanges.Count > 0)
                            {
                                line = $"{sqlCommand} INTO `ace_object_animation_change` (`aceObjectId`, `index`, `animationId`)" + Environment.NewLine;

                                foreach (var animation in parsed.objdesc.apChanges)
                                {
                                    if (once)
                                    {
                                        line += $"     , ({parsed.wdesc._wcid}, {animation.part_index}, {animation.part_id})" + Environment.NewLine;
                                    }
                                    else
                                    {
                                        line += $"VALUES ({parsed.wdesc._wcid}, {animation.part_index}, {animation.part_id})" + Environment.NewLine;
                                        once = true;
                                    }
                                }

                                line = line.TrimEnd(Environment.NewLine.ToCharArray()) + ";" + Environment.NewLine;
                                writer.WriteLine(line);
                            }
                        }
                    }
                }
            }
        }

        private string ConvertMovementBufferToString(byte[] movement_buffer)
        {
            if (movement_buffer == null)
                return "0";
            else
                //return Encoding.UTF8.GetString(movement_buffer, 0, movement_buffer.Length);
                return Convert.ToBase64String(movement_buffer);
        }

        private bool PositionRecorded(CM_Physics.CreateObject parsed, List<Position> positions, Position newPosition, float margin = 0.02f)
        {
            if (newPosition?.frame?.m_fOrigin == null)
                return true; // can't dedupe this

            return positions.Any(p => p.objcell_id == newPosition.objcell_id
                                && Math.Abs(p.frame.m_fOrigin.x - newPosition.frame.m_fOrigin.x) < margin
                                && Math.Abs(p.frame.m_fOrigin.y - newPosition.frame.m_fOrigin.y) < margin
                                && Math.Abs(p.frame.m_fOrigin.z - newPosition.frame.m_fOrigin.z) < margin);
        }

        private void WriteUniqueTypes(CM_Physics.CreateObject parsed, StreamWriter writer, List<ITEM_TYPE> itemTypesToParse, Dictionary<ITEM_TYPE, List<string>> itemTypeKeys)
        {
            TreeView treeView = new TreeView();

            if (!itemTypesToParse.Contains(parsed.wdesc._type))
                return;

            totalHits++;

            // This bit of trickery uses the existing tree view parser code to create readable output, which we can then convert to csv
            treeView.Nodes.Clear();
            parsed.contributeToTreeView(treeView);
            if (treeView.Nodes.Count == 1)
            {
                var lineItems = new string[256];
                int lineItemCount = 0;

                ProcessNode(treeView.Nodes[0], itemTypeKeys[parsed.wdesc._type], null, lineItems, ref lineItemCount);

                var sb = new StringBuilder();

                for (int i = 0; i < lineItemCount; i++)
                {
                    if (i > 0)
                        sb.Append(',');

                    var output = lineItems[i];

                    // Format the value for CSV output, if needed.
                    // We only do this for certain columns. This is very time consuming
                    if (output != null && itemTypeKeys[parsed.wdesc._type][i].EndsWith("name"))
                    {
                        if (output.Contains(",") || output.Contains("\"") || output.Contains("\r") || output.Contains("\n"))
                        {
                            var sb2 = new StringBuilder();
                            sb2.Append("\"");
                            foreach (char nextChar in output)
                            {
                                sb2.Append(nextChar);
                                if (nextChar == '"')
                                    sb2.Append("\"");
                            }
                            sb2.Append("\"");
                            output = sb2.ToString();
                        }

                    }

                    if (output != null)
                        sb.Append(output);
                }

                writer.WriteLine(sb.ToString());
            }
        }

        private void ProcessNode(TreeNode node, List<string> keys, string prefix, string[] lineItems, ref int lineItemCount)
        {
            var kvp = ConvertNodeTextToKVP(node.Text);

            var nodeKey = (prefix == null ? kvp.Key : (prefix + "." + kvp.Key));

            // ********************************************************************
            // ***************** YOU CAN OMIT CERTAIN NODES HERE ****************** 
            // ********************************************************************
            //if (nodeKey.StartsWith("physicsdesc.timestamps")) return;

            if (node.Nodes.Count == 0)
            {
                if (!keys.Contains(nodeKey))
                    keys.Add(nodeKey);

                var keyIndex = keys.IndexOf(nodeKey);

                if (keyIndex >= lineItems.Length)
                    MessageBox.Show("Increase the lineItems array size");

                lineItems[keyIndex] = kvp.Value;

                if (keyIndex + 1 > lineItemCount)
                    lineItemCount = keyIndex + 1;
            }
            else
            {
                foreach (TreeNode child in node.Nodes)
                    ProcessNode(child, keys, nodeKey, lineItems, ref lineItemCount);
            }
        }

        private static KeyValuePair<string, string> ConvertNodeTextToKVP(string nodeText)
        {
            string key = null;
            string value = null;

            var indexOfEquals = nodeText.IndexOf('=');

            if (indexOfEquals == -1)
                value = nodeText;
            else
            {
                key = nodeText.Substring(0, indexOfEquals).Trim();

                if (nodeText.Length > indexOfEquals + 1)
                    value = nodeText.Substring(indexOfEquals + 1, nodeText.Length - indexOfEquals - 1).Trim();
            }

            return new KeyValuePair<string, string>(key, value);
        }


        private void timer1_Tick(object sender, EventArgs e)
        {
            toolStripStatusLabel1.Text = "Files Processed: " + filesProcessed.ToString("N0") + " of " + filesToProcess.Count.ToString("N0");

            toolStripStatusLabel2.Text = "Fragments Processed: " + fragmentsProcessed.ToString("N0");

            toolStripStatusLabel3.Text = "Total Hits: " + totalHits.ToString("N0");

            toolStripStatusLabel4.Text = "Frag Exceptions: " + totalExceptions.ToString("N0");
        }
    }
}
