﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

using aclogview.Properties;

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
        private readonly FragDatListFile createObjectAppraisalInfoFragDatFile = new FragDatListFile();

        private void DoBuild()
        {
            // ********************************************************************
            // ************************ Adjust These Paths ************************ 
            // ********************************************************************
            allFragDatFile.CreateFile(Path.Combine(txtOutputFolder.Text, "All.frags"), chkCompressOutput.Checked ? FragDatListFile.CompressionType.DeflateStream : FragDatListFile.CompressionType.None);
            createObjectFragDatFile.CreateFile(Path.Combine(txtOutputFolder.Text, "CreateObject.frags"), chkCompressOutput.Checked ? FragDatListFile.CompressionType.DeflateStream : FragDatListFile.CompressionType.None);
            appraisalInfoFragDatFile.CreateFile(Path.Combine(txtOutputFolder.Text, "AppraisalInfo.frags"), chkCompressOutput.Checked ? FragDatListFile.CompressionType.DeflateStream : FragDatListFile.CompressionType.None);
            createObjectAppraisalInfoFragDatFile.CreateFile(Path.Combine(txtOutputFolder.Text, "CreateObjectPlusAppraisalInfo.frags"), chkCompressOutput.Checked ? FragDatListFile.CompressionType.DeflateStream : FragDatListFile.CompressionType.None);

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
            createObjectAppraisalInfoFragDatFile.CloseFile();
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
            var createObjectPlusAppraisalInfoFrags = new List<FragDatListFile.FragDatInfo>();

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
                    if (messageCode == 0xF745) // Create Object
                    {
                        Interlocked.Increment(ref totalHits);

                        //createObjectFrags.Add(new FragDatListFile.FragDatInfo(packetDirection, record.index, frag.dat_));
                        createObjectFrags.Add(new FragDatListFile.FragDatInfo(packetDirection, record.index, record.data));

                        //createObjectPlusAppraisalInfoFrags.Add(new FragDatListFile.FragDatInfo(packetDirection, record.index, frag.dat_));
                        createObjectPlusAppraisalInfoFrags.Add(new FragDatListFile.FragDatInfo(packetDirection, record.index, record.data));
                    }

                    if (messageCode == 0x00C9) // APPRAISAL_INFO_EVENT
                    {
                        Interlocked.Increment(ref totalHits);

                        //appraisalInfoFrags.Add(new FragDatListFile.FragDatInfo(packetDirection, record.index, frag.dat_));
                        appraisalInfoFrags.Add(new FragDatListFile.FragDatInfo(packetDirection, record.index, record.data));

                        //createObjectPlusAppraisalInfoFrags.Add(new FragDatListFile.FragDatInfo(packetDirection, record.index, frag.dat_));
                        createObjectPlusAppraisalInfoFrags.Add(new FragDatListFile.FragDatInfo(packetDirection, record.index, record.data));
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
            createObjectAppraisalInfoFragDatFile.Write(new KeyValuePair<string, IList<FragDatListFile.FragDatInfo>>(outputFileName, createObjectPlusAppraisalInfoFrags));

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
                List<uint> objectIds = new List<uint>();
                Dictionary<string, List<CM_Physics.CreateObject>> weenies = new Dictionary<string, List<CM_Physics.CreateObject>>();
                Dictionary<string, List<CM_Physics.CreateObject>> staticObjects = new Dictionary<string, List<CM_Physics.CreateObject>>();
                Dictionary<uint, List<Position>> processedWeeniePositions = new Dictionary<uint, List<Position>>();

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

                            if (messageCode == 0xF745) // Create Object
                            {
                                var parsed = CM_Physics.CreateObject.read(fragDataReader);
                                
                                CreateStaticObjectsList(parsed, objectIds, staticObjects, weenieIds, weenies, processedWeeniePositions);

                                //totalHits++;

                                //if (!itemTypesToParse.Contains(parsed.wdesc._type))
                                //    continue;

                                //totalHits++;

                                //// This bit of trickery uses the existing tree view parser code to create readable output, which we can then convert to csv
                                //treeView.Nodes.Clear();
                                //parsed.contributeToTreeView(treeView);
                                //if (treeView.Nodes.Count == 1)
                                //{
                                //    var lineItems = new string[256];
                                //    int lineItemCount = 0;

                                //    ProcessNode(treeView.Nodes[0], itemTypeKeys[parsed.wdesc._type], null, lineItems, ref lineItemCount);

                                //    var sb = new StringBuilder();

                                //    for (int i = 0; i < lineItemCount; i++)
                                //    {
                                //        if (i > 0)
                                //            sb.Append(',');

                                //        var output = lineItems[i];

                                //        // Format the value for CSV output, if needed.
                                //        // We only do this for certain columns. This is very time consuming
                                //        if (output != null && itemTypeKeys[parsed.wdesc._type][i].EndsWith("name"))
                                //        {
                                //            if (output.Contains(",") || output.Contains("\"") || output.Contains("\r") || output.Contains("\n"))
                                //            {
                                //                var sb2 = new StringBuilder();
                                //                sb2.Append("\"");
                                //                foreach (char nextChar in output)
                                //                {
                                //                    sb2.Append(nextChar);
                                //                    if (nextChar == '"')
                                //                        sb2.Append("\"");
                                //                }
                                //                sb2.Append("\"");
                                //                output = sb2.ToString();
                                //            }

                                //        }

                                //        if (output != null)
                                //            sb.Append(output);
                                //    }

                                //    itemTypeStreamWriters[parsed.wdesc._type].WriteLine(sb.ToString());
                                //}
                            }
                        }
                        catch (EndOfStreamException) // This can happen when a frag is incomplete and we try to parse it
                        {
                            totalExceptions++;
                        }
                    }
                }

                WriteWeenieData(weenies, txtOutputFolder.Text);

                WriteStaticObjectData(staticObjects, txtOutputFolder.Text);

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

        private void CreateStaticObjectsList(CM_Physics.CreateObject parsed, List<uint> objectIds, Dictionary<string, List<CM_Physics.CreateObject>> staticObjects, List<uint> weenieIds, Dictionary<string, List<CM_Physics.CreateObject>> weenies, Dictionary<uint, List<Position>> processedWeeniePositions)
        {
            try
            {
                // don't need undefined crap or players
                if (parsed.wdesc._wcid == 1 || objectIds.Contains(parsed.object_id))
                    return;

                bool addIt = false;
                bool addWeenie = false;
                string fileToPutItIn = "Other";
                float margin = 0.02f;

                if (parsed.wdesc._type == ITEM_TYPE.TYPE_MISC) // HOUSE OBJECTS
                {
                    if (
                           parsed.wdesc._wcid == 9548                                 || // W_HOUSE_CLASS
                           parsed.wdesc._wcid >= 9693  && parsed.wdesc._wcid <= 10492 || // W_HOUSECOTTAGE1_CLASS to W_HOUSECOTTAGE800_CLASS
                           parsed.wdesc._wcid >= 10493 && parsed.wdesc._wcid <= 10662 || // W_HOUSEVILLA801_CLASS to W_HOUSEVILLA970_CLASS
                           parsed.wdesc._wcid >= 10663 && parsed.wdesc._wcid <= 10692 || // W_HOUSEMANSION971_CLASS to W_HOUSEMANSION1000_CLASS
                           parsed.wdesc._wcid >= 10746 && parsed.wdesc._wcid <= 10750 || // W_HOUSETEST1_CLASS to W_HOUSETEST5_CLASS
                           parsed.wdesc._wcid >= 10829 && parsed.wdesc._wcid <= 10839 || // W_HOUSETEST6_CLASS to W_HOUSETEST16_CLASS
                           parsed.wdesc._wcid >= 11677 && parsed.wdesc._wcid <= 11682 || // W_HOUSETEST17_CLASS to W_HOUSETEST22_CLASS
                           parsed.wdesc._wcid >= 12311 && parsed.wdesc._wcid <= 12460 || // W_HOUSECOTTAGE1001_CLASS to W_HOUSECOTTAGE1150_CLASS
                           parsed.wdesc._wcid >= 12775 && parsed.wdesc._wcid <= 13024 || // W_HOUSECOTTAGE1151_CLASS to W_HOUSECOTTAGE1400_CLASS
                           parsed.wdesc._wcid >= 13025 && parsed.wdesc._wcid <= 13064 || // W_HOUSEVILLA1401_CLASS to W_HOUSEVILLA1440_CLASS
                           parsed.wdesc._wcid >= 13065 && parsed.wdesc._wcid <= 13074 || // W_HOUSEMANSION1441_CLASS to W_HOUSEMANSION1450_CLASS
                           parsed.wdesc._wcid == 13234                                || // W_HOUSECOTTAGETEST10000_CLASS
                           parsed.wdesc._wcid == 13235                                || // W_HOUSEVILLATEST10001_CLASS
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
                        fileToPutItIn = "Houses";
                        addIt = true;
                    }
                }
                else if (parsed.wdesc._type == ITEM_TYPE.TYPE_PORTAL) // HOUSE PORTALS
                {
                    if (
                        parsed.wdesc._wcid == 9620  || // W_PORTALHOUSE_CLASS
                        parsed.wdesc._wcid == 10751 || // W_PORTALHOUSETEST_CLASS
                        parsed.wdesc._wcid == 11730    // W_HOUSEPORTAL_CLASS
                        )
                    {
                        fileToPutItIn = "HousePortals";
                        addIt = true;
                    }
                }
                else if (parsed.wdesc._type == ITEM_TYPE.TYPE_CONTAINER) // HOOKS AND STORAGE
                {
                    if (
                        parsed.wdesc._wcid == 9686  && parsed.wdesc._name.ToString().Contains("Hook") || // W_HOOK_CLASS
                        parsed.wdesc._wcid == 11697 && parsed.wdesc._name.ToString().Contains("Hook") || // W_HOOK_FLOOR_CLASS
                        parsed.wdesc._wcid == 11698 && parsed.wdesc._name.ToString().Contains("Hook") || // W_HOOK_CEILING_CLASS
                        parsed.wdesc._wcid == 12678 && parsed.wdesc._name.ToString().Contains("Hook") || // W_HOOK_ROOF_CLASS
                        parsed.wdesc._wcid == 12679 && parsed.wdesc._name.ToString().Contains("Hook") || // W_HOOK_YARD_CLASS
                        parsed.wdesc._wcid == 9687     // W_STORAGE_CLASS
                        )
                    {
                        fileToPutItIn = "HooksStorage";
                        addIt = true;
                    }
                }
                else if (parsed.wdesc._type == ITEM_TYPE.TYPE_UNDEF) // SLUMLORD OBJECTS
                {
                    if (
                        parsed.wdesc._wcid == 9621  || // W_SLUMLORD_CLASS
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
                        fileToPutItIn = "SlumLords";
                        if (parsed.wdesc._name.m_buffer.Contains("'s Cottage"))
                            parsed.wdesc._name.m_buffer = "Cottage";
                        if (parsed.wdesc._name.m_buffer.Contains("'s Villa"))
                            parsed.wdesc._name.m_buffer = "Villa";
                        if (parsed.wdesc._name.m_buffer.Contains("'s Mansion"))
                            parsed.wdesc._name.m_buffer = "Mansion";
                        if (parsed.wdesc._name.m_buffer.Contains("'s Apartment"))
                            parsed.wdesc._name.m_buffer = "Apartment";
                        addIt = true;
                    }
                }

                //if (parsed.wdesc._name.m_buffer.Contains("Door")
                //    && parsed.wdesc._type == ITEM_TYPE.TYPE_MISC)
                //{
                //    fileToPutItIn = "Doors";
                //    addIt = true;
                //}
                //else if (parsed.wdesc._name.m_buffer == "Sign"
                //    && parsed.wdesc._type == ITEM_TYPE.TYPE_MISC)
                //{
                //    fileToPutItIn = "Signs";
                //    addIt = true;
                //}
                //else if (parsed.wdesc._name.m_buffer == "Statue"
                //    && parsed.wdesc._type == ITEM_TYPE.TYPE_MISC)
                //{
                //    fileToPutItIn = "Statues";
                //    addIt = true;
                //}
                //else if (parsed.wdesc._name.m_buffer == "Lever"
                //    && parsed.wdesc._type == ITEM_TYPE.TYPE_MISC)
                //{
                //    fileToPutItIn = "Levers";
                //    addIt = true;
                //}
                //else if (parsed.wdesc._name.m_buffer == "Town Crier"
                //    && parsed.wdesc._type == ITEM_TYPE.TYPE_CREATURE
                //    && parsed.wdesc._blipColor == 8)
                //{
                //    fileToPutItIn = "Town Criers";
                //    addIt = true;
                //    margin = 15f;
                //}
                //else if (parsed.wdesc._type == ITEM_TYPE.TYPE_PORTAL
                //    && !(parsed.wdesc._blipColor == 3 || parsed.wdesc._name.m_buffer == "Gateway")) // exclude white / player summoned portals
                //{
                //    fileToPutItIn = "Portals";
                //    addIt = true;
                //}
                //else if ((parsed.wdesc._name.m_buffer.Contains("Scrivener")
                //        || parsed.wdesc._name.m_buffer.Contains("Scribe")
                //        || parsed.wdesc._name.m_buffer.Contains("Archmage")
                //        || parsed.wdesc._name.m_buffer.Contains("Healer")
                //        || parsed.wdesc._name.m_buffer.Contains("Weaponsmith")
                //        || parsed.wdesc._name.m_buffer.Contains("Weapons Master")
                //        || parsed.wdesc._name.m_buffer.Contains("Armorer")
                //        || parsed.wdesc._name.m_buffer.Contains("Grocer")
                //        || parsed.wdesc._name.m_buffer.Contains("Shopkeep")
                //        || parsed.wdesc._name.m_buffer.Contains("Shopkeeper")
                //        || parsed.wdesc._name.m_buffer.Contains("Jeweler")
                //        || parsed.wdesc._name.m_buffer.Contains("Barkeep")
                //        || parsed.wdesc._name.m_buffer.Contains("Barkeeper")
                //        || parsed.wdesc._name.m_buffer.Contains("Provisioner")
                //        || parsed.wdesc._name.m_buffer.Contains("Tailor")
                //        || parsed.wdesc._name.m_buffer.Contains("Seamstress")
                //        || parsed.wdesc._name.m_buffer.Contains("Fletcher")
                //        || parsed.wdesc._name.m_buffer.Contains("Bowyer")
                //        || parsed.wdesc._name.m_buffer.Contains("Marksman")
                //        || parsed.wdesc._name.m_buffer.Contains("Crafter")
                //        || parsed.wdesc._name.m_buffer.Contains("Cook")
                //        || parsed.wdesc._name.m_buffer.Contains("Alchemist")
                //        || parsed.wdesc._name.m_buffer.Contains("Woodsman"))
                //    && parsed.wdesc._type == ITEM_TYPE.TYPE_CREATURE
                //    && parsed.wdesc._blipColor == 8)
                //{
                //    fileToPutItIn = "Vendors";
                //    addIt = true;
                //}
                //else if ((parsed.wdesc._name.m_buffer == "Agent of the Arcanum"
                //        || parsed.wdesc._name.m_buffer == "Sentry"
                //        || parsed.wdesc._name.m_buffer == "Ulgrim the Unpleasant"
                //        || parsed.wdesc._name.m_buffer == "Wedding Planner"
                //        || parsed.wdesc._name.m_buffer.Contains("Collector")
                //        || parsed.wdesc._name.m_buffer.Contains("Guard")
                //        || parsed.wdesc._name.m_buffer == "Farmer")
                //    && parsed.wdesc._type == ITEM_TYPE.TYPE_CREATURE
                //    && parsed.wdesc._blipColor == 8)
                //{
                //    fileToPutItIn = "OtherNPCs";
                //    addIt = true;
                //}
                //else if (parsed.wdesc._type == ITEM_TYPE.TYPE_CREATURE && parsed.wdesc._blipColor == 8)
                //{
                //    fileToPutItIn = "UnknownNPCs";
                //    addIt = true;
                //}
                //else if (parsed.wdesc._type == ITEM_TYPE.TYPE_CREATURE && parsed.wdesc._blipColor == 2)
                //{
                //    fileToPutItIn = "MonsterWeenies";
                //    addWeenie = true;
                //}
                //else if (parsed.object_id < 0x80000000)
                //{
                //    fileToPutItIn = "PotentialStatics";
                //    addIt = true;
                //}

                //if (((uint)parsed.wdesc._type & (uint)ITEM_TYPE.TYPE_ITEM) > 0)
                //{
                //    fileToPutItIn = "ItemWeenies";
                //    addWeenie = true;
                //}
                //else
                //{
                //    fileToPutItIn = "OtherWeenies";
                //    addWeenie = true;
                //}

                if (!processedWeeniePositions.ContainsKey(parsed.wdesc._wcid))
                    processedWeeniePositions.Add(parsed.wdesc._wcid, new List<Position>());

                // de-dupe based on position and wcid
                //if (addIt && !PositionRecorded(parsed, processedWeeniePositions[parsed.wdesc._wcid], parsed.physicsdesc.pos, margin))
                if (addIt) //&& !PositionRecorded(parsed, processedWeeniePositions[parsed.wdesc._wcid], parsed.physicsdesc.pos, margin))
                {
                    if (!weenieIds.Contains(parsed.wdesc._wcid))
                    {
                        if (!weenies.ContainsKey(fileToPutItIn))
                            weenies.Add(fileToPutItIn, new List<CM_Physics.CreateObject>());

                        weenies[fileToPutItIn].Add(parsed);
                        weenieIds.Add(parsed.wdesc._wcid);
                    }

                    if (!staticObjects.ContainsKey(fileToPutItIn))
                        staticObjects.Add(fileToPutItIn, new List<CM_Physics.CreateObject>());

                    staticObjects[fileToPutItIn].Add(parsed);
                    objectIds.Add(parsed.object_id);

                    processedWeeniePositions[parsed.wdesc._wcid].Add(parsed.physicsdesc.pos);

                    totalHits++;
                }
                else if (addWeenie)
                {
                    if (!weenieIds.Contains(parsed.wdesc._wcid))
                    {
                        if (!weenies.ContainsKey(fileToPutItIn))
                            weenies.Add(fileToPutItIn, new List<CM_Physics.CreateObject>());

                        weenies[fileToPutItIn].Add(parsed);
                        weenieIds.Add(parsed.wdesc._wcid);

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

        private void WriteStaticObjectData(Dictionary<string, List<CM_Physics.CreateObject>> staticObjects, string outputFolder)
        {
            string staticFolder = Path.Combine(outputFolder, "objects");

            if (!Directory.Exists(staticFolder))
                Directory.CreateDirectory(staticFolder);

            Dictionary<ITEM_TYPE, int> fileCount = new Dictionary<ITEM_TYPE, int>();

            foreach (string key in staticObjects.Keys)
            {
                string filename = Path.Combine(staticFolder, $"{key}.sql");

                if (File.Exists(filename))
                    File.Delete(filename);

                foreach (var parsed in staticObjects[key])
                {
                    try
                    {
                        string fullFile = Path.Combine(staticFolder, $"{filename}");

                        //if (File.Exists(fullFile))
                        //{
                        //    FileInfo fi = new FileInfo(fullFile);

                        //    // go to the next file if it's bigger than a MB
                        //    if (fi.Length > (1048576))
                        //    {
                        //        fileCount[parsed.wdesc._type]++;
                        //        fullFile = Path.Combine(staticFolder, $"{parsed.wdesc._type}_{fileCount[parsed.wdesc._type]}.sql");
                        //    }
                        //}

                        using (FileStream fs = new FileStream(fullFile, FileMode.Append))
                        {
                            using (StreamWriter writer = new StreamWriter(fs))
                            {
                                string line = "INSERT INTO `base_ace_object` (`baseAceObjectId`, `name`, `typeId`, `paletteId`, " +

                                // wdesc data
                                "`ammoType`, `blipColor`, `bitField`, `burden`, `combatUse`, `cooldownDuration`, " +
                                "`cooldownId`, `effects`, `containersCapacity`, `header`, `hookTypeId`, `iconId`, `iconOverlayId`, " +
                                "`iconUnderlayId`, `hookItemTypes`, `itemsCapacity`, `location`, `materialType`, " +
                                "`maxStackSize`, `maxStructure`, `radar`, `pscript`, `spellId`, `stackSize`, " +
                                "`structure`, `targetTypeId`, `usability`, `useRadius`, `validLocations`, `value`, " +
                                "`workmanship`, " +

                                // physics data
                                "`animationFrameId`, `defaultScript`, `defaultScriptIntensity`, `elasticity`, " +
                                "`friction`, `locationId`, `modelTableId`, `objectScale`, `physicsBitField`, " +
                                "`physicsTableId`, `motionTableId`, `soundTableId`, `physicsState`, `translucency`)" + Environment.NewLine + "VALUES (" +

                                // shove the wcid in here so we can tell the difference between weenie classes and real objects for analysis
                                $"{parsed.object_id}, '{parsed.wdesc._name.m_buffer.Replace("'", "''")}', {(uint)parsed.wdesc._type}, {parsed.objdesc.paletteID}, " +

                                // wdesc data
                                $"{(uint)parsed.wdesc._ammoType}, {parsed.wdesc._blipColor}, {parsed.wdesc._bitfield}, {parsed.wdesc._burden}, {parsed.wdesc._combatUse}, {parsed.wdesc._cooldown_duration}, " +
                                $"{parsed.wdesc._cooldown_id}, {parsed.wdesc._effects}, {parsed.wdesc._containersCapacity}, {parsed.wdesc.header}, {(uint)parsed.wdesc._hook_type}, {parsed.wdesc._iconID}, {parsed.wdesc._iconOverlayID}, " +
                                $"{parsed.wdesc._iconUnderlayID}, {parsed.wdesc._hook_item_types}, {parsed.wdesc._itemsCapacity}, {parsed.wdesc._location}, {(uint)parsed.wdesc._material_type}, " +
                                $"{parsed.wdesc._maxStackSize}, {parsed.wdesc._maxStructure}, {(uint)parsed.wdesc._radar_enum}, {parsed.wdesc._pscript}, {parsed.wdesc._spellID}, {parsed.wdesc._stackSize}, " +
                                $"{parsed.wdesc._structure}, {(uint)parsed.wdesc._targetType}, {(uint)parsed.wdesc._useability}, {parsed.wdesc._useRadius}, {parsed.wdesc._valid_locations}, {parsed.wdesc._value}, " +
                                $"{parsed.wdesc._workmanship}, " +

                                // physics data.  note, model table is mis-parsed as setup_id.  the setup_id is actually "mtable", which is presumably motion table id.
                                $"{parsed.physicsdesc.animframe_id}, {(uint)parsed.physicsdesc.default_script}, {parsed.physicsdesc.default_script_intensity}, {parsed.physicsdesc.elasticity}, " +
                                $"{parsed.physicsdesc.friction}, {parsed.physicsdesc.location_id}, {parsed.physicsdesc.setup_id}, {parsed.physicsdesc.object_scale}, {parsed.physicsdesc.bitfield}, " +
                                $"{parsed.physicsdesc.phstable_id}, {parsed.physicsdesc.mtable_id}, {parsed.physicsdesc.stable_id}, {(uint)parsed.physicsdesc.state}, {parsed.physicsdesc.translucency});" + Environment.NewLine;

                                // creates the weenieClass record
                                writer.WriteLine(line);

                                line = "INSERT INTO `ace_object` (`baseAceObjectId`, `weenieClassId`, `landblock`, `cell`, `posX`, `posY`, `posZ`, `qW`, `qX`, `qY`, `qZ`)" + Environment.NewLine +
                                    $"VALUES ({parsed.object_id}, {parsed.wdesc._wcid}, {parsed.physicsdesc.pos.objcell_id >> 16}, {parsed.physicsdesc.pos.objcell_id & 0xFFFF}, " +
                                    $"{parsed.physicsdesc.pos.frame.m_fOrigin.x}, {parsed.physicsdesc.pos.frame.m_fOrigin.y}, {parsed.physicsdesc.pos.frame.m_fOrigin.z}, " +
                                    $"{parsed.physicsdesc.pos.frame.qw}, {parsed.physicsdesc.pos.frame.qx}, {parsed.physicsdesc.pos.frame.qy}, {parsed.physicsdesc.pos.frame.qz});" + Environment.NewLine;

                                writer.WriteLine(line);

                                bool once = false;
                                if (parsed.objdesc.subpalettes.Count > 0)
                                {
                                    line = "INSERT INTO `ace_object_palette_changes` (`baseAceObjectId`, `subPaletteId`, `offset`, `length`)" + Environment.NewLine;

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
                                    line = "INSERT INTO `ace_object_texture_map_changes` (`baseAceObjectId`, `index`, `oldId`, `newId`)" + Environment.NewLine;

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
                                    line = "INSERT INTO `ace_object_animation_changes` (`baseAceObjectId`, `index`, `animationId`)" + Environment.NewLine;

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

        private void WriteWeenieData(Dictionary<string, List<CM_Physics.CreateObject>> weenies, string outputFolder)
        {
            string templateFolder = Path.Combine(outputFolder, "weenies");

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
                    using (FileStream fs = new FileStream(filename, FileMode.Append))
                    {
                        using (StreamWriter writer = new StreamWriter(fs))
                        {
                            string line = "INSERT INTO `base_ace_object` (`baseAceObjectId`, `name`, `typeId`, `paletteId`, " +

                            // wdesc data
                            "`ammoType`, `blipColor`, `bitField`, `burden`, `combatUse`, `cooldownDuration`, " +
                            "`cooldownId`, `effects`, `containersCapacity`, `header`, `hookTypeId`, `iconId`, `iconOverlayId`, " +
                            "`iconUnderlayId`, `hookItemTypes`, `itemsCapacity`, `location`, `materialType`, " +
                            "`maxStackSize`, `maxStructure`, `radar`, `pscript`, `spellId`, `stackSize`, " +
                            "`structure`, `targetTypeId`, `usability`, `useRadius`, `validLocations`, `value`, " +
                            "`workmanship`, " +

                            // physics data
                            "`animationFrameId`, `defaultScript`, `defaultScriptIntensity`, `elasticity`, " +
                            "`friction`, `locationId`, `modelTableId`, `objectScale`, `physicsBitField`, " +
                            "`physicsTableId`, `motionTableId`, `soundTableId`, `physicsState`, `translucency`)" + Environment.NewLine + "VALUES (" +

                            // shove the wcid in here so we can tell the difference between weenie classes and real objects for analysis
                            $"{parsed.wdesc._wcid}, '{parsed.wdesc._name.m_buffer.Replace("'", "''")}', {(uint)parsed.wdesc._type}, {parsed.objdesc.paletteID}, " +

                            // wdesc data
                            $"{(uint)parsed.wdesc._ammoType}, {parsed.wdesc._blipColor}, {parsed.wdesc._bitfield}, {parsed.wdesc._burden}, {parsed.wdesc._combatUse}, {parsed.wdesc._cooldown_duration}, " +
                            $"{parsed.wdesc._cooldown_id}, {parsed.wdesc._effects}, {parsed.wdesc._containersCapacity}, {parsed.wdesc.header}, {(uint)parsed.wdesc._hook_type}, {parsed.wdesc._iconID}, {parsed.wdesc._iconOverlayID}, " +
                            $"{parsed.wdesc._iconUnderlayID}, {parsed.wdesc._hook_item_types}, {parsed.wdesc._itemsCapacity}, {parsed.wdesc._location}, {(uint)parsed.wdesc._material_type}, " +
                            $"{parsed.wdesc._maxStackSize}, {parsed.wdesc._maxStructure}, {(uint)parsed.wdesc._radar_enum}, {parsed.wdesc._pscript}, {parsed.wdesc._spellID}, {parsed.wdesc._stackSize}, " +
                            $"{parsed.wdesc._structure}, {(uint)parsed.wdesc._targetType}, {(uint)parsed.wdesc._useability}, {parsed.wdesc._useRadius}, {parsed.wdesc._valid_locations}, {parsed.wdesc._value}, " +
                            $"{parsed.wdesc._workmanship}, " +

                            // physics data.  note, model table is mis-parsed as setup_id.  the setup_id is actually "mtable", which is presumably motion table id.
                            $"{parsed.physicsdesc.animframe_id}, {(uint)parsed.physicsdesc.default_script}, {parsed.physicsdesc.default_script_intensity}, {parsed.physicsdesc.elasticity}, " +
                            $"{parsed.physicsdesc.friction}, {parsed.physicsdesc.location_id}, {parsed.physicsdesc.setup_id}, {parsed.physicsdesc.object_scale}, {parsed.physicsdesc.bitfield}, " +
                            $"{parsed.physicsdesc.phstable_id}, {parsed.physicsdesc.mtable_id}, {parsed.physicsdesc.stable_id}, {(uint)parsed.physicsdesc.state}, {parsed.physicsdesc.translucency});" + Environment.NewLine;

                            // creates the base ace object record
                            writer.WriteLine(line);

                            line = "INSERT INTO weenie_class (`weenieClassId`, `baseAceObjectId`)" + Environment.NewLine +
                                $"VALUES ({parsed.wdesc._wcid}, {parsed.wdesc._wcid});" + Environment.NewLine;
                            writer.WriteLine(line);

                            once = false;
                            if (parsed.objdesc.subpalettes.Count > 0)
                            {
                                line = "INSERT INTO `weenie_palette_changes` (`weenieClassId`, `subPaletteId`, `offset`, `length`)" + Environment.NewLine;

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
                                line = "INSERT INTO `weenie_texture_map_changes` (`weenieClassId`, `index`, `oldId`, `newId`)" + Environment.NewLine;

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
                                line = "INSERT INTO `weenie_animation_changes` (`weenieClassId`, `index`, `animationId`)" + Environment.NewLine;

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
