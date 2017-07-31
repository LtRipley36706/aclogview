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
                    if (messageCode == (uint)PacketOpcode.Evt_Physics__CreateObject_ID) // Create Object
                    {
                        Interlocked.Increment(ref totalHits);

                        //createObjectFrags.Add(new FragDatListFile.FragDatInfo(packetDirection, record.index, frag.dat_));
                        createObjectFrags.Add(new FragDatListFile.FragDatInfo(packetDirection, record.index, record.data));

                        //createObjectPlusAppraisalInfoFrags.Add(new FragDatListFile.FragDatInfo(packetDirection, record.index, frag.dat_));
                        createObjectPlusAppraisalInfoFrags.Add(new FragDatListFile.FragDatInfo(packetDirection, record.index, record.data));
                    }

                    if (messageCode == (uint)PacketOpcode.APPRAISAL_INFO_EVENT) // APPRAISAL_INFO_EVENT
                    {
                        Interlocked.Increment(ref totalHits);

                        //appraisalInfoFrags.Add(new FragDatListFile.FragDatInfo(packetDirection, record.index, frag.dat_));
                        appraisalInfoFrags.Add(new FragDatListFile.FragDatInfo(packetDirection, record.index, record.data));

                        //createObjectPlusAppraisalInfoFrags.Add(new FragDatListFile.FragDatInfo(packetDirection, record.index, frag.dat_));
                        createObjectPlusAppraisalInfoFrags.Add(new FragDatListFile.FragDatInfo(packetDirection, record.index, record.data));
                    }

                    if (messageCode == (uint)PacketOpcode.WEENIE_ORDERED_EVENT || messageCode == (uint)PacketOpcode.ORDERED_EVENT) // WEENIE_ORDERED_EVENT or ORDERED_EVENT 
                    {
                        uint opCode = 0;
                        //uint Hdr = 0;

                        // Hdr = fragDataReader.Read();
                        //opCode = fragDataReader.ReadUInt32();

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

                        if (opCode == (uint)PacketOpcode.APPRAISAL_INFO_EVENT)
                        {
                            Interlocked.Increment(ref totalHits);

                            //appraisalInfoFrags.Add(new FragDatListFile.FragDatInfo(packetDirection, record.index, frag.dat_));
                            appraisalInfoFrags.Add(new FragDatListFile.FragDatInfo(packetDirection, record.index, record.data));

                            //createObjectPlusAppraisalInfoFrags.Add(new FragDatListFile.FragDatInfo(packetDirection, record.index, frag.dat_));
                            createObjectPlusAppraisalInfoFrags.Add(new FragDatListFile.FragDatInfo(packetDirection, record.index, record.data));
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

                List<uint> appraisalObjectIds = new List<uint>();
                Dictionary<string, List<CM_Examine.SetAppraiseInfo>> appraisalObjects = new Dictionary<string, List<CM_Examine.SetAppraiseInfo>>();
                //Dictionary<uint, uint> assessmentInfoStatus = new Dictionary<uint, uint>();
                Dictionary<uint, string> appraisalObjectsCatagoryMap = new Dictionary<uint, string>();
                Dictionary<uint, uint> appraisalObjectToWeenieId = new Dictionary<uint, uint>();

                Dictionary<uint, uint> staticObjectsWeenieType = new Dictionary<uint, uint>();
                Dictionary<uint, uint> weeniesWeenieType = new Dictionary<uint, uint>();

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

                        //if (fragmentsProcessed > 600000)
                        //    break;

                        try
                        {
                            // ********************************************************************
                            // ********************** CUSTOM PROCESSING CODE ********************** 
                            // ********************************************************************
                            if (frag.Data.Length <= 4)
                                continue;

                            BinaryReader fragDataReader = new BinaryReader(new MemoryStream(frag.Data));

                            var messageCode = fragDataReader.ReadUInt32();

                            if (messageCode == (uint)PacketOpcode.Evt_Physics__CreateObject_ID) // Create Object
                            {
                                var parsed = CM_Physics.CreateObject.read(fragDataReader);

                                CreateStaticObjectsList(parsed, objectIds, staticObjects, weenieIds, weenies, processedWeeniePositions, appraisalObjectsCatagoryMap, appraisalObjectToWeenieId, weeniesWeenieType, staticObjectsWeenieType);

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

                            if (messageCode == (uint)PacketOpcode.Evt_Physics__CreateObject_ID) // Create Object
                            {
                                var parsed = CM_Physics.CreateObject.read(fragDataReader);

                                CreateStaticObjectsList(parsed, objectIds, staticObjects, weenieIds, weenies, processedWeeniePositions, appraisalObjectsCatagoryMap, appraisalObjectToWeenieId, weeniesWeenieType, staticObjectsWeenieType);                                      
                            }
                        }
                        catch (EndOfStreamException) // This can happen when a frag is incomplete and we try to parse it
                        {
                            totalExceptions++;
                        }
                    }

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

                            if (messageCode == (uint)PacketOpcode.WEENIE_ORDERED_EVENT || messageCode == (uint)PacketOpcode.ORDERED_EVENT) // WEENIE_ORDERED_EVENT or ORDERED_EVENT 
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

                                if (opCode == (uint)PacketOpcode.APPRAISAL_INFO_EVENT)
                                {
                                    var parsed = CM_Examine.SetAppraiseInfo.read(fragDataReader);

                                    CreateAppraisalObjectsList(parsed, objectIds, staticObjects, weenieIds, weenies, appraisalObjects, appraisalObjectIds, appraisalObjectsCatagoryMap, appraisalObjectToWeenieId);
                                }
                            }
                        }
                        catch (EndOfStreamException) // This can happen when a frag is incomplete and we try to parse it
                        {
                            totalExceptions++;
                        }
                    }

                }

                WriteWeenieData(weenies, txtOutputFolder.Text, weeniesWeenieType);

                WriteWeenieAppraisalObjectData(appraisalObjects, appraisalObjectIds, appraisalObjectToWeenieId, txtOutputFolder.Text);

                WriteStaticObjectData(staticObjects, objectIds, txtOutputFolder.Text, staticObjectsWeenieType);

                WriteAppraisalObjectData(appraisalObjects, appraisalObjectIds, appraisalObjectToWeenieId, txtOutputFolder.Text);

                //// WriteGeneratorObjectData(staticObjects, objectIds, txtOutputFolder.Text);

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

        private void CreateAppraisalObjectsList(CM_Examine.SetAppraiseInfo parsed, List<uint> objectIds, Dictionary<string, List<CM_Physics.CreateObject>> staticObjects, List<uint> weenieIds, Dictionary<string, List<CM_Physics.CreateObject>> weenies, Dictionary<string, List<CM_Examine.SetAppraiseInfo>> appraisalObjects, List<uint> appraisalObjectIds, Dictionary<uint, string> appraisalObjectsCatagoryMap, Dictionary<uint, uint> appraisalObjectToWeenieId)
        {
            try
            {
                //uint success = 0;
                // don't need undefined crap or players
                //if (!objectIds.Contains(parsed.i_objid) || (assessmentInfoIds.Contains(parsed.i_objid) && assessmentInfoStatus.TryGetValue(parsed.i_objid, out success)))
                //if (!objectIds.Contains(parsed.i_objid) || assessmentInfoIds.Contains(parsed.i_objid))
                uint weenieId = 0;
                bool foundInObjectIds = false;
                bool foundInWeenieIds = false;
                foundInObjectIds = objectIds.Contains(parsed.i_objid);
                appraisalObjectToWeenieId.TryGetValue(parsed.i_objid, out weenieId);
                foundInWeenieIds = weenieIds.Contains(weenieId);

                //if (!objectIds.Contains(parsed.i_objid) || weenieId > 0 )
                //if (!foundInObjectIds || weenieId > 0)
                //    return;

                if (!foundInObjectIds && !(weenieId > 0))
                    return;

                //if (parsed.wdesc._wcid == 1 || objectIds.Contains(parsed.object_id))
                //    return;

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

                    parsed.i_objid = weenieId;
                }

                // de-dupe based on position and wcid
                //if (addIt && !PositionRecorded(parsed, processedWeeniePositions[parsed.wdesc._wcid], parsed.physicsdesc.pos, margin))
                if (addIt) //&& !PositionRecorded(parsed, processedWeeniePositions[parsed.wdesc._wcid], parsed.physicsdesc.pos, margin))
                {
                    //if (!weenieIds.Contains(parsed.wdesc._wcid))
                    //{
                    //    if (!weenies.ContainsKey(fileToPutItIn))
                    //        weenies.Add(fileToPutItIn, new List<CM_Physics.CreateObject>());

                    //    weenies[fileToPutItIn].Add(parsed);
                    //    weenieIds.Add(parsed.wdesc._wcid);
                    //}

                    if (!appraisalObjects.ContainsKey(fileToPutItIn))
                        appraisalObjects.Add(fileToPutItIn, new List<CM_Examine.SetAppraiseInfo>());

                    if (appraisalObjectIds.Contains(parsed.i_objid))
                    {
                        //int i = appraisalObjects[fileToPutItIn].FindIndex(appraisalObjects.Values);
                        //var keyToRemove = appraisalObjects.FirstOrDefault(x => x.Value..FindIndex(parsed.i_objid)).Key;
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
                        //if (appraisalObjects[fileToPutItIn][appraisalObjectIds.IndexOf(parsed.i_objid)].i_prof.success_flag == 0)
                        {
                            //assessmentInfo[fileToPutItIn].Remove(parsed);
                            //appraisalObjects[fileToPutItIn].RemoveAt(appraisalObjectIds.IndexOf(parsed.i_objid));
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

                        //parsedClone = parsed;
                        parsedClone = new CM_Examine.SetAppraiseInfo();
                        parsedClone.i_objid = weenieId;
                        parsedClone.i_prof = parsed.i_prof;

                        parsedClone.i_objid = weenieId;
                        appraisalObjects[fileToPutItIn].Add(parsedClone);
                        appraisalObjectIds.Add(parsedClone.i_objid);
                    }
                    //assessmentInfoStatus.Add(parsed.i_objid, parsed.i_prof.success_flag);

                    //processedWeeniePositions[parsed.wdesc._wcid].Add(parsed.physicsdesc.pos);
                    //if (parsed.i_objid == 3688325436)
                    //{
                    //    System.Diagnostics.Debug.WriteLine("Found Axe");
                    //}

                    totalHits++;
                }
                //else if (addWeenie)
                //{
                //    if (!weenieIds.Contains(parsed.wdesc._wcid))
                //    {
                //        if (!weenies.ContainsKey(weeniefileToPutItIn))
                //            weenies.Add(weeniefileToPutItIn, new List<CM_Physics.CreateObject>());

                //        weenies[weeniefileToPutItIn].Add(parsed);
                //        weenieIds.Add(parsed.wdesc._wcid);

                //        totalHits++;
                //    }
                //}
            }
            catch (Exception ex)
            {
                // MessageBox.Show(ex.ToString());
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

        private void WriteAppraisalObjectData(Dictionary<string, List<CM_Examine.SetAppraiseInfo>> appraisalObjects, List<uint> appraisalObjectIds, Dictionary<uint, uint> appraisalObjectToWeenieId, string outputFolder)
        {
            string staticFolder = Path.Combine(outputFolder, "4-apprasialobjects");

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

        private void WriteWeenieAppraisalObjectData(Dictionary<string, List<CM_Examine.SetAppraiseInfo>> appraisalObjects, List<uint> appraisalObjectIds, Dictionary<uint, uint> appraisalObjectToWeenieId, string outputFolder)
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

        private void CreateStaticObjectsList(CM_Physics.CreateObject parsed, List<uint> objectIds, Dictionary<string, List<CM_Physics.CreateObject>> staticObjects, List<uint> weenieIds, Dictionary<string, List<CM_Physics.CreateObject>> weenies, Dictionary<uint, List<Position>> processedWeeniePositions, Dictionary<uint, string> appraisalObjectsCatagoryMap, Dictionary<uint, uint> appraisalObjectToWeenieId, Dictionary<uint, uint> weeniesWeenieType, Dictionary<uint, uint> staticObjectsWeenieType)
        {
            try
            {
                // don't need undefined crap or players
                //if (parsed.wdesc._wcid == 1 || objectIds.Contains(parsed.object_id))
                if (objectIds.Contains(parsed.object_id))
                    return;

                bool addIt = false;
                bool addWeenie = false;
                string fileToPutItIn = "Other";
                string weeniefileToPutItIn = "OtherWeenies";
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
                    weeniefileToPutItIn = "Players";
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
                    weeniefileToPutItIn = "Lockpicks";
                    weenieType = WeenieType.Lockpick_WeenieType;
                    addWeenie = true;
                }
                else if ((parsed.wdesc._bitfield & (uint)CM_Physics.PublicWeenieDesc.BitfieldIndex.BF_FOOD) != 0)
                {
                    weeniefileToPutItIn = "FoodObjects";
                    weenieType = WeenieType.Food_WeenieType;
                    addWeenie = true;
                }
                else if ((parsed.wdesc._bitfield & (uint)CM_Physics.PublicWeenieDesc.BitfieldIndex.BF_HEALER) != 0)
                {
                    weeniefileToPutItIn = "Healers";
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
                        weeniefileToPutItIn = "BooksScrolls";
                        weenieType = WeenieType.Scroll_WeenieType;
                        addWeenie = true;
                    }
                    else if (parsed.wdesc._name.m_buffer.Contains("Pack"))
                    {
                        weeniefileToPutItIn = "BooksPackToys";
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
                        weeniefileToPutItIn = "Books";
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
                        weeniefileToPutItIn = "PortalsSummoned";
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
                        weeniefileToPutItIn = "VendorsOlthoiPlayers";
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
                        weeniefileToPutItIn = "VendorsPets";
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
                    weeniefileToPutItIn = "Admins";
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
                        weenieType = WeenieType.Book_WeenieType;
                        addIt = true;
                    }
                    else if (parsed.wdesc._name.m_buffer.Contains("Residential Halls"))
                    {
                        fileToPutItIn = "MiscResidentialHallSigns";
                        weenieType = WeenieType.Book_WeenieType;
                        addIt = true;
                    }
                    else if (parsed.wdesc._name.m_buffer.Contains("Deed"))
                    {
                        weeniefileToPutItIn = "HouseDeeds";
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
                        || parsed.wdesc._name.m_buffer.Contains("Hollow")
                        )
                    {
                        fileToPutItIn = "MiscResidentialHallSigns";
                        weenieType = WeenieType.Book_WeenieType;
                        addIt = true;
                    }
                    else if (parsed.wdesc._name.m_buffer.Contains("Festival Stone"))
                    {
                        fileToPutItIn = "MiscFestivalStones";
                        weenieType = WeenieType.Book_WeenieType;
                        addIt = true;
                    }
                    else if (parsed.physicsdesc.setup_id == 33557463
                        )
                    {
                        fileToPutItIn = "MiscSettlementMarkers";
                        weenieType = WeenieType.Book_WeenieType;
                        parsed.physicsdesc.bitfield = 32769; // error correct
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
                        weeniefileToPutItIn = "MiscObjects";
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
                        weeniefileToPutItIn = "PortalsSummoned";
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
                    if (
                        parsed.wdesc._wcid == 9686 && parsed.wdesc._name.m_buffer.Contains("Hook") || // W_HOOK_CLASS
                        parsed.wdesc._wcid == 11697 && parsed.wdesc._name.m_buffer.Contains("Hook") || // W_HOOK_FLOOR_CLASS
                        parsed.wdesc._wcid == 11698 && parsed.wdesc._name.m_buffer.Contains("Hook") || // W_HOOK_CEILING_CLASS
                        parsed.wdesc._wcid == 12678 && parsed.wdesc._name.m_buffer.Contains("Hook") || // W_HOOK_ROOF_CLASS
                        parsed.wdesc._wcid == 12679 && parsed.wdesc._name.m_buffer.Contains("Hook") // W_HOOK_YARD_CLASS
                        )
                    {
                        fileToPutItIn = "HouseHooks";
                        weenieType = WeenieType.Hook_WeenieType;
                        addIt = true;
                    }
                    else if (
                            parsed.wdesc._wcid == 9686 || // W_HOOK_CLASS
                            parsed.wdesc._wcid == 11697 || // W_HOOK_FLOOR_CLASS
                            parsed.wdesc._wcid == 11698 || // W_HOOK_CEILING_CLASS
                            parsed.wdesc._wcid == 12678 || // W_HOOK_ROOF_CLASS
                            parsed.wdesc._wcid == 12679  // W_HOOK_YARD_CLASS
                            )
                    {
                        weeniefileToPutItIn = "HouseHooks";
                        weenieType = WeenieType.Hook_WeenieType;
                        if (parsed.wdesc._wcid == 9686)
                            parsed.wdesc._name.m_buffer = "Wall Hook";
                        if (parsed.wdesc._wcid == 11697)
                            parsed.wdesc._name.m_buffer = "Floor Hook";
                        if (parsed.wdesc._wcid == 11698)
                            parsed.wdesc._name.m_buffer = "Ceiling Hook";
                        if (parsed.wdesc._wcid == 12678)
                            parsed.wdesc._name.m_buffer = "Roof Hook";
                        if (parsed.wdesc._wcid == 12679)
                            parsed.wdesc._name.m_buffer = "Yard Hook";
                        addWeenie = true;
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
                        weeniefileToPutItIn = "ContainersCorpses";
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
                        weeniefileToPutItIn = "ContainersPacks";
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
                        weeniefileToPutItIn = "Containers";
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
                        if (parsed.wdesc._name.m_buffer.Contains("'s Cottage"))
                            parsed.wdesc._name.m_buffer = "Cottage";
                        if (parsed.wdesc._name.m_buffer.Contains("'s Villa"))
                            parsed.wdesc._name.m_buffer = "Villa";
                        if (parsed.wdesc._name.m_buffer.Contains("'s Mansion"))
                            parsed.wdesc._name.m_buffer = "Mansion";
                        if (parsed.wdesc._name.m_buffer.Contains("'s Apartment"))
                            parsed.wdesc._name.m_buffer = "Apartment";

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
                        weeniefileToPutItIn = "Generators";
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
                        weeniefileToPutItIn = "Generators";
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
                        weeniefileToPutItIn = "ProjectileSpellObjects";
                        weenieType = WeenieType.ProjectileSpell_WeenieType;
                        addWeenie = true;
                    }
                    else if (parsed.wdesc._name.m_buffer.Contains("Missile")
                            || parsed.wdesc._name.m_buffer.Contains("Egg")
                            || parsed.wdesc._name.m_buffer.Contains("Snowball")
                    )
                    {
                        weeniefileToPutItIn = "MissileObjects";
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
                        weeniefileToPutItIn = "UndefObjects";
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
                        weeniefileToPutItIn = "WriteablesScrolls";
                        weenieType = WeenieType.Scroll_WeenieType;
                        addWeenie = true;
                    }
                    else if (parsed.wdesc._name.m_buffer.Contains("Pack"))
                    {
                        weeniefileToPutItIn = "WriteablesPackToys";
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
                        weeniefileToPutItIn = "WritableObjects";
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
                        weeniefileToPutItIn = "CreaturesOlthoiPlayers";
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
                        weeniefileToPutItIn = "CreaturesPets";
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
                        weeniefileToPutItIn = "CreaturesMonsters";
                        weenieType = WeenieType.Creature_WeenieType;
                        addWeenie = true;
                    }
                    else if (parsed.object_id < 0x80000000)
                    {
                        fileToPutItIn = "CreaturesNPCStatics";
                        weenieType = WeenieType.Creature_WeenieType;
                        addIt = true;
                    }
                    else if (parsed.wdesc._name.m_buffer.Contains("Statue")
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
                        weeniefileToPutItIn = "CreaturesCows";
                        weenieType = WeenieType.Cow_WeenieType;
                        addWeenie = true;
                    }
                    //else if (parsed.wdesc._name.m_buffer.Contains("Auroch"))
                    //{
                    //    weeniefileToPutItIn = "CreaturesAurochs";
                    //    weenieType = WeenieType.Cow_WeenieType;
                    //    addWeenie = true;
                    //}
                    else
                    {
                        weeniefileToPutItIn = "CreaturesUnsorted";
                        weenieType = WeenieType.Creature_WeenieType;
                        addWeenie = true;
                    }
                }
                else if (parsed.object_id < 0x80000000)
                {
                    fileToPutItIn = "LandscapeStatics";
                    weenieType = WeenieType.Generic_WeenieType;
                    addIt = true;
                }
                else if (parsed.wdesc._type == ITEM_TYPE.TYPE_ARMOR)
                {
                    weeniefileToPutItIn = "Armor";
                    weenieType = WeenieType.Clothing_WeenieType;
                    addWeenie = true;
                }
                else if (parsed.wdesc._type == ITEM_TYPE.TYPE_MELEE_WEAPON)
                {
                    weeniefileToPutItIn = "MeleeWeapons";
                    weenieType = WeenieType.MeleeWeapon_WeenieType;
                    addWeenie = true;
                }
                else if (parsed.wdesc._type == ITEM_TYPE.TYPE_CLOTHING)
                {
                    weeniefileToPutItIn = "Clothing";
                    weenieType = WeenieType.Clothing_WeenieType;
                    addWeenie = true;
                }
                else if (parsed.wdesc._type == ITEM_TYPE.TYPE_JEWELRY)
                {
                    weeniefileToPutItIn = "Jewelry";
                    weenieType = WeenieType.Clothing_WeenieType;
                    addWeenie = true;
                }
                else if (parsed.wdesc._type == ITEM_TYPE.TYPE_FOOD)
                {
                    weeniefileToPutItIn = "Food";
                    weenieType = WeenieType.Food_WeenieType;
                    addWeenie = true;
                }
                else if (parsed.wdesc._type == ITEM_TYPE.TYPE_MONEY)
                {
                    weeniefileToPutItIn = "Money";
                    weenieType = WeenieType.Coin_WeenieType;
                    addWeenie = true;
                }
                else if (parsed.wdesc._type == ITEM_TYPE.TYPE_MISSILE_WEAPON)
                {
                    weeniefileToPutItIn = "MissileWeapons";
                    weenieType = WeenieType.MissileLauncher_WeenieType;
                    addWeenie = true;
                }
                else if (parsed.wdesc._type == ITEM_TYPE.TYPE_GEM)
                {
                    weeniefileToPutItIn = "Gems";
                    weenieType = WeenieType.Gem_WeenieType;
                    addWeenie = true;
                }
                else if (parsed.wdesc._type == ITEM_TYPE.TYPE_SPELL_COMPONENTS)
                {
                    weeniefileToPutItIn = "SpellComponents";
                    weenieType = WeenieType.SpellComponent_WeenieType;
                    addWeenie = true;
                }
                else if (parsed.wdesc._type == ITEM_TYPE.TYPE_KEY)
                {
                    weeniefileToPutItIn = "Keys";
                    weenieType = WeenieType.Key_WeenieType;
                    addWeenie = true;
                }
                else if (parsed.wdesc._type == ITEM_TYPE.TYPE_CASTER)
                {
                    weeniefileToPutItIn = "Casters";
                    weenieType = WeenieType.Caster_WeenieType;
                    addWeenie = true;
                }
                else if (parsed.wdesc._type == ITEM_TYPE.TYPE_MANASTONE)
                {
                    weeniefileToPutItIn = "ManaStones";
                    weenieType = WeenieType.ManaStone_WeenieType;
                    addWeenie = true;
                }
                else if (parsed.wdesc._type == ITEM_TYPE.TYPE_PROMISSORY_NOTE)
                {
                    weeniefileToPutItIn = "PromissoryNotes";
                    weenieType = WeenieType.Generic_WeenieType;
                    addWeenie = true;
                }
                else if (parsed.wdesc._type == ITEM_TYPE.TYPE_CRAFT_ALCHEMY_BASE)
                {
                    weeniefileToPutItIn = "CraftAlchemyBase";
                    weenieType = WeenieType.Generic_WeenieType;
                    addWeenie = true;
                }
                else if (parsed.wdesc._type == ITEM_TYPE.TYPE_CRAFT_ALCHEMY_INTERMEDIATE)
                {
                    weeniefileToPutItIn = "CraftAlchemyIntermediate";
                    weenieType = WeenieType.Generic_WeenieType;
                    addWeenie = true;
                }
                else if (parsed.wdesc._type == ITEM_TYPE.TYPE_CRAFT_COOKING_BASE)
                {
                    weeniefileToPutItIn = "CraftCookingBase";
                    weenieType = WeenieType.Generic_WeenieType;
                    addWeenie = true;
                }
                else if (parsed.wdesc._type == ITEM_TYPE.TYPE_CRAFT_FLETCHING_BASE)
                {
                    weeniefileToPutItIn = "CraftFletchingBase";
                    weenieType = WeenieType.Generic_WeenieType;
                    addWeenie = true;
                }
                else if (parsed.wdesc._type == ITEM_TYPE.TYPE_CRAFT_FLETCHING_INTERMEDIATE)
                {
                    weeniefileToPutItIn = "CraftFletchingIntermediate";
                    weenieType = WeenieType.Generic_WeenieType;
                    addWeenie = true;
                }
                else if (parsed.wdesc._type == ITEM_TYPE.TYPE_TINKERING_TOOL)
                {
                    weeniefileToPutItIn = "TinkeringTools";
                    weenieType = WeenieType.CraftTool_WeenieType;
                    addWeenie = true;
                }
                else if (parsed.wdesc._type == ITEM_TYPE.TYPE_TINKERING_MATERIAL)
                {
                    weeniefileToPutItIn = "TinkeringMaterials";
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
                    weeniefileToPutItIn = "ItemsUnsorted";
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
                    weeniefileToPutItIn = "LightSourceObjects";
                    weenieType = WeenieType.LightSource_WeenieType;
                    addWeenie = true;
                }
                else
                {
                    weeniefileToPutItIn = "OtherObjects";
                    weenieType = WeenieType.Generic_WeenieType;
                    addWeenie = true;
                }

                // }

                if (!processedWeeniePositions.ContainsKey(parsed.wdesc._wcid))
                    processedWeeniePositions.Add(parsed.wdesc._wcid, new List<Position>());

                if (objectIds.Contains(parsed.wdesc._wielderID))
                {
                    fileToPutItIn = weeniefileToPutItIn + "-wielded";
                    addIt = true;
                }
                else if (objectIds.Contains(parsed.wdesc._containerID))
                {
                    fileToPutItIn = weeniefileToPutItIn + "-contained";
                    addIt = true;
                }
                else if (objectIds.Contains(parsed.physicsdesc.parent_id))
                {
                    fileToPutItIn = weeniefileToPutItIn + "-children";
                    addIt = true;
                }

                // de-dupe based on position and wcid
                if (addIt && !PositionRecorded(parsed, processedWeeniePositions[parsed.wdesc._wcid], parsed.physicsdesc.pos, margin))
                // if (addIt) //&& !PositionRecorded(parsed, processedWeeniePositions[parsed.wdesc._wcid], parsed.physicsdesc.pos, margin))
                {
                    if (!weenieIds.Contains(parsed.wdesc._wcid))
                    {
                        if (!weenies.ContainsKey(fileToPutItIn))
                            weenies.Add(fileToPutItIn, new List<CM_Physics.CreateObject>());

                        weenies[fileToPutItIn].Add(parsed);
                        weenieIds.Add(parsed.wdesc._wcid);

                        if (!appraisalObjectsCatagoryMap.ContainsKey(parsed.object_id))
                            appraisalObjectsCatagoryMap.Add(parsed.object_id, fileToPutItIn);
                        if (!appraisalObjectToWeenieId.ContainsKey(parsed.object_id))
                            appraisalObjectToWeenieId.Add(parsed.object_id, parsed.wdesc._wcid);

                        if (!weeniesWeenieType.ContainsKey(parsed.wdesc._wcid))
                            weeniesWeenieType.Add(parsed.wdesc._wcid, (uint)weenieType);
                    }

                    if (!staticObjects.ContainsKey(fileToPutItIn))
                        staticObjects.Add(fileToPutItIn, new List<CM_Physics.CreateObject>());

                    staticObjects[fileToPutItIn].Add(parsed);
                    objectIds.Add(parsed.object_id);

                    if (!appraisalObjectsCatagoryMap.ContainsKey(parsed.object_id))
                        appraisalObjectsCatagoryMap.Add(parsed.object_id, fileToPutItIn);

                    if (!staticObjectsWeenieType.ContainsKey(parsed.object_id))
                        staticObjectsWeenieType.Add(parsed.object_id, (uint)weenieType);

                    processedWeeniePositions[parsed.wdesc._wcid].Add(parsed.physicsdesc.pos);

                    totalHits++;
                }
                else if (addWeenie)
                {
                    if (!weenieIds.Contains(parsed.wdesc._wcid))
                    {
                        if (!weenies.ContainsKey(weeniefileToPutItIn))
                            weenies.Add(weeniefileToPutItIn, new List<CM_Physics.CreateObject>());

                        weenies[weeniefileToPutItIn].Add(parsed);
                        weenieIds.Add(parsed.wdesc._wcid);

                        if (!appraisalObjectsCatagoryMap.ContainsKey(parsed.object_id))
                            appraisalObjectsCatagoryMap.Add(parsed.object_id, weeniefileToPutItIn);
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

        private void WriteStaticObjectData(Dictionary<string, List<CM_Physics.CreateObject>> staticObjects, List<uint> objectIds, string outputFolder, Dictionary<uint, uint> staticObjectsWeenieType)
        {
            string staticFolder = Path.Combine(outputFolder, "3-objects");

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

                                //string line = $"{sqlCommand} INTO `ace_object` (`" +
                                //"aceObjectId`, `aceObjectDescriptionFlags`, " +
                                //"`weenieClassId`, `weenieHeaderFlags`, " +
                                //"`iconId`, `iconOverlayId`, `iconUnderlayId`, " +
                                //"`modelTableId`, `soundTableId`, " +
                                //"`motionTableId`, `currentMotionState`, `animationFrameId`, " +
                                //"`physicsTableId`, `physicsDescriptionFlag`, " +
                                //"`spellId`, " +
                                //"`playScript`, `defaultScript`" +
                                //")" + Environment.NewLine + "VALUES (" +

                                string line = $"{sqlCommand} INTO `ace_object` (`" +
                                "aceObjectId`, `aceObjectDescriptionFlags`, " +
                                "`weenieClassId`, `weenieHeaderFlags`, " +
                                "`currentMotionState`, " +
                                "`physicsDescriptionFlag`" +
                                ")" + Environment.NewLine + "VALUES (" +

                                //$"{parsed.object_id}, {parsed.wdesc.header}, " +
                                //$"{parsed.wdesc._wcid}, {parsed.wdesc._bitfield}, "; //+
                                $"{parsed.object_id}, {parsed.wdesc._bitfield}, " +
                                $"{parsed.wdesc._wcid}, {parsed.wdesc.header}, "; //+
                                //$"{parsed.wdesc._iconID}, ";
                                didsLine += $"     , ({parsed.object_id}, {(uint)STypeDID.ICON_DID}, {(uint)parsed.wdesc._iconID})" + Environment.NewLine;
                                if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_IconOverlay) != 0)
                                    //    line += $"{parsed.wdesc._iconOverlayID}, ";
                                    didsLine += $"     , ({parsed.object_id}, {(uint)STypeDID.ICON_OVERLAY_DID}, {(uint)parsed.wdesc._iconOverlayID})" + Environment.NewLine;
                                //else
                                //    line += $"NULL, ";
                                if ((parsed.wdesc.header2 & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader2.PWD2_Packed_IconUnderlay) != 0)
                                    //    line += $"{parsed.wdesc._iconUnderlayID}, ";
                                    didsLine += $"     , ({parsed.object_id}, {(uint)STypeDID.ICON_UNDERLAY_DID}, {(uint)parsed.wdesc._iconUnderlayID})" + Environment.NewLine;
                                //else
                                //    line += $"NULL, ";

                                if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.CSetup) != 0)
                                    //    line += $"{parsed.physicsdesc.setup_id}, ";
                                    didsLine += $"     , ({parsed.object_id}, {(uint)STypeDID.SETUP_DID}, {(uint)parsed.physicsdesc.setup_id})" + Environment.NewLine;
                                //else
                                //    line += $"NULL, ";
                                if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.STABLE) != 0)
                                    //    line += $"{parsed.physicsdesc.stable_id}, ";
                                    didsLine += $"     , ({parsed.object_id}, {(uint)STypeDID.SOUND_TABLE_DID}, {(uint)parsed.physicsdesc.stable_id})" + Environment.NewLine;
                                //else
                                //    line += $"NULL, ";
                                if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.MTABLE) != 0)
                                    //    line += $"{parsed.physicsdesc.mtable_id}, ";
                                    didsLine += $"     , ({parsed.object_id}, {(uint)STypeDID.MOTION_TABLE_DID}, {(uint)parsed.physicsdesc.mtable_id})" + Environment.NewLine;
                                //else
                                //    line += $"NULL, ";
                                if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.MOVEMENT) != 0)
                                    line += $"'{ConvertMovementBufferToString(parsed.physicsdesc.movement_buffer)}', ";
                                else
                                    line += $"NULL, ";
                                if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.ANIMFRAME_ID) != 0)
                                    //    line += $"{parsed.physicsdesc.animframe_id}, ";
                                    intsLine += $"     , ({parsed.object_id}, {(uint)STypeInt.PLACEMENT_POSITION_INT}, {(uint)parsed.physicsdesc.animframe_id})" + Environment.NewLine;
                                //else
                                //    line += $"NULL, ";
                                if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.PETABLE) != 0)
                                    //    line += $"{parsed.physicsdesc.phstable_id}, ";
                                    didsLine += $"     , ({parsed.object_id}, {(uint)STypeDID.PHYSICS_EFFECT_TABLE_DID}, {(uint)parsed.physicsdesc.phstable_id})" + Environment.NewLine;
                                //else
                                //    line += $"NULL, ";
                                //line += $"{parsed.physicsdesc.bitfield}, ";
                                line += $"{parsed.physicsdesc.bitfield}";
                                if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_SpellID) != 0)
                                    //    line += $"{parsed.wdesc._spellID}, ";
                                    didsLine += $"     , ({parsed.object_id}, {(uint)STypeDID.SPELL_DID}, {(uint)parsed.wdesc._spellID})" + Environment.NewLine;
                                //else
                                //    line += $"NULL, ";
                                if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_PScript) != 0)
                                    //    line += $"{parsed.wdesc._pscript}, ";
                                    didsLine += $"     , ({parsed.object_id}, {(uint)STypeDID.PHYSICS_SCRIPT_DID}, {(uint)parsed.wdesc._pscript})" + Environment.NewLine;
                                //else
                                //    line += $"NULL, ";
                                if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.DEFAULT_SCRIPT) != 0)
                                    //    line += $"{(uint)parsed.physicsdesc.default_script}";
                                    didsLine += $"     , ({parsed.object_id}, {(uint)STypeDID.USE_USER_ANIMATION_DID}, {(uint)parsed.physicsdesc.default_script})" + Environment.NewLine;
                                //else
                                //    line += $"NULL";

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
                                weenieName = "ace" + parsed.wdesc._wcid.ToString() + "-" + parsed.wdesc._name.m_buffer.Replace("'", "").Replace(" ", "").Replace(".", "").Replace("(", "").Replace(")", "").Replace("+", "").Replace(":", "").Replace("_", "").Replace("-", "").ToLower();

                            string line = $"{sqlCommand} INTO ace_weenie_class (`weenieClassId`, `weenieClassDescription`)" + Environment.NewLine +
                                   $"VALUES ({parsed.wdesc._wcid}, '{weenieName}');" + Environment.NewLine;
                            writer.WriteLine(line);

                            string intsLine = "", bigintsLine = "", floatsLine = "", boolsLine = "", strsLine = "", didsLine = "", iidsLine = "";

                            //line = $"{sqlCommand} INTO `ace_object` (`" +
                            //"aceObjectId`, `aceObjectDescriptionFlags`, " +
                            //"`weenieClassId`, `weenieHeaderFlags`, " +
                            //"`iconId`, `iconOverlayId`, `iconUnderlayId`, " +
                            //"`modelTableId`, `soundTableId`, " +
                            //"`motionTableId`, `currentMotionState`, `animationFrameId`, " +
                            //"`physicsTableId`, `physicsDescriptionFlag`, " +
                            //"`spellId`, " +
                            //"`playScript`, `defaultScript`" +
                            //")" + Environment.NewLine + "VALUES (" +

                            //$"{parsed.wdesc._wcid}, {parsed.wdesc.header}, " +
                            //$"{parsed.wdesc._wcid}, {parsed.wdesc._bitfield}, " +
                            //$"{parsed.wdesc._iconID}, ";
                            //if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_IconOverlay) != 0)
                            //    line += $"{parsed.wdesc._iconOverlayID}, ";
                            //else
                            //    line += $"NULL, ";
                            //if ((parsed.wdesc.header2 & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader2.PWD2_Packed_IconUnderlay) != 0)
                            //    line += $"{parsed.wdesc._iconUnderlayID}, ";
                            //else
                            //    line += $"NULL, ";

                            //if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.CSetup) != 0)
                            //    line += $"{parsed.physicsdesc.setup_id}, ";
                            //else
                            //    line += $"NULL, ";
                            //if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.STABLE) != 0)
                            //    line += $"{parsed.physicsdesc.stable_id}, ";
                            //else
                            //    line += $"NULL, ";
                            //if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.MTABLE) != 0)
                            //    line += $"{parsed.physicsdesc.mtable_id}, ";
                            //else
                            //    line += $"NULL, ";
                            //if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.MOVEMENT) != 0)
                            //    line += $"'{ConvertMovementBufferToString(parsed.physicsdesc.movement_buffer)}', ";
                            //else
                            //    line += $"NULL, ";
                            //if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.ANIMFRAME_ID) != 0)
                            //    line += $"{parsed.physicsdesc.animframe_id}, ";
                            //else
                            //    line += $"NULL, ";
                            //if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.PETABLE) != 0)
                            //    line += $"{parsed.physicsdesc.phstable_id}, ";
                            //else
                            //    line += $"NULL, ";
                            //line += $"{parsed.physicsdesc.bitfield}, ";
                            //if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_SpellID) != 0)
                            //    line += $"{parsed.wdesc._spellID}, ";
                            //else
                            //    line += $"NULL, ";
                            //if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_PScript) != 0)
                            //    line += $"{parsed.wdesc._pscript}, ";
                            //else
                            //    line += $"NULL, ";
                            //if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.DEFAULT_SCRIPT) != 0)
                            //    line += $"{(uint)parsed.physicsdesc.default_script}";
                            //else
                            //    line += $"NULL";

                            //line += ");" + Environment.NewLine;

                            //// creates the weenieClass record
                            //writer.WriteLine(line);

                            line = $"{sqlCommand} INTO `ace_object` (`" +
                                "aceObjectId`, `aceObjectDescriptionFlags`, " +
                                "`weenieClassId`, `weenieHeaderFlags`, " +
                                "`currentMotionState`, " +
                                "`physicsDescriptionFlag`" +
                                ")" + Environment.NewLine + "VALUES (" +

                            //$"{parsed.wdesc._wcid}, {parsed.wdesc.header}, " +
                            //$"{parsed.wdesc._wcid}, {parsed.wdesc._bitfield}, "; //+
                            $"{parsed.wdesc._wcid}, {parsed.wdesc._bitfield}, " +
                            $"{parsed.wdesc._wcid}, {parsed.wdesc.header}, "; //+
                            //$"{parsed.wdesc._iconID}, ";
                            didsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeDID.ICON_DID}, {(uint)parsed.wdesc._iconID})" + Environment.NewLine;
                            if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_IconOverlay) != 0)
                                //    line += $"{parsed.wdesc._iconOverlayID}, ";
                                didsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeDID.ICON_OVERLAY_DID}, {(uint)parsed.wdesc._iconOverlayID})" + Environment.NewLine;
                            //else
                            //    line += $"NULL, ";
                            if ((parsed.wdesc.header2 & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader2.PWD2_Packed_IconUnderlay) != 0)
                                //    line += $"{parsed.wdesc._iconUnderlayID}, ";
                                didsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeDID.ICON_UNDERLAY_DID}, {(uint)parsed.wdesc._iconUnderlayID})" + Environment.NewLine;
                            //else
                            //    line += $"NULL, ";

                            if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.CSetup) != 0)
                                //    line += $"{parsed.physicsdesc.setup_id}, ";
                                didsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeDID.SETUP_DID}, {(uint)parsed.physicsdesc.setup_id})" + Environment.NewLine;
                            //else
                            //    line += $"NULL, ";
                            if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.STABLE) != 0)
                                //    line += $"{parsed.physicsdesc.stable_id}, ";
                                didsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeDID.SOUND_TABLE_DID}, {(uint)parsed.physicsdesc.stable_id})" + Environment.NewLine;
                            //else
                            //    line += $"NULL, ";
                            if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.MTABLE) != 0)
                                //    line += $"{parsed.physicsdesc.mtable_id}, ";
                                didsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeDID.MOTION_TABLE_DID}, {(uint)parsed.physicsdesc.mtable_id})" + Environment.NewLine;
                            //else
                            //    line += $"NULL, ";
                            if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.MOVEMENT) != 0)
                                line += $"'{ConvertMovementBufferToString(parsed.physicsdesc.movement_buffer)}', ";
                            else
                                line += $"NULL, ";
                            if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.ANIMFRAME_ID) != 0)
                                //    line += $"{parsed.physicsdesc.animframe_id}, ";
                                intsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeInt.PLACEMENT_POSITION_INT}, {(uint)parsed.physicsdesc.animframe_id})" + Environment.NewLine;
                            //else
                            //    line += $"NULL, ";
                            if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.PETABLE) != 0)
                                //    line += $"{parsed.physicsdesc.phstable_id}, ";
                                didsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeDID.PHYSICS_EFFECT_TABLE_DID}, {(uint)parsed.physicsdesc.phstable_id})" + Environment.NewLine;
                            //else
                            //    line += $"NULL, ";
                            //line += $"{parsed.physicsdesc.bitfield}, ";
                            line += $"{parsed.physicsdesc.bitfield}";
                            if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_SpellID) != 0)
                                //    line += $"{parsed.wdesc._spellID}, ";
                                didsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeDID.SPELL_DID}, {(uint)parsed.wdesc._spellID})" + Environment.NewLine;
                            //else
                            //    line += $"NULL, ";
                            if ((parsed.wdesc.header & (uint)PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_PScript) != 0)
                                //    line += $"{parsed.wdesc._pscript}, ";
                                didsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeDID.PHYSICS_SCRIPT_DID}, {(uint)parsed.wdesc._pscript})" + Environment.NewLine;
                            //else
                            //    line += $"NULL, ";
                            if ((parsed.physicsdesc.bitfield & (uint)PhysicsDesc.PhysicsDescInfo.DEFAULT_SCRIPT) != 0)
                                //    line += $"{(uint)parsed.physicsdesc.default_script}";
                                didsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeDID.USE_USER_ANIMATION_DID}, {(uint)parsed.physicsdesc.default_script})" + Environment.NewLine;
                            //else
                            //    line += $"NULL";

                            line += ");" + Environment.NewLine;

                            writer.WriteLine(line);

                            //string intsLine = "", bigintsLine = "", floatsLine = "", boolsLine = "", strsLine = "";

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
                            if (((uint)parsed.wdesc._bitfield & (uint)PublicWeenieDesc.BitfieldIndex.BF_PLAYER_KILLER) != 0)
                                boolsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeBool.PK_KILLER_BOOL}, {true})" + Environment.NewLine;
                            if (((uint)parsed.wdesc._bitfield & (uint)PublicWeenieDesc.BitfieldIndex.BF_REQUIRES_PACKSLOT) != 0)
                                boolsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeBool.REQUIRES_BACKPACK_SLOT_BOOL}, {true})" + Environment.NewLine;
                            if (((uint)parsed.wdesc._bitfield & (uint)PublicWeenieDesc.BitfieldIndex.BF_RETAINED) != 0)
                                boolsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeBool.RETAINED_BOOL}, {true})" + Environment.NewLine;
                            if (((uint)parsed.wdesc._bitfield & (uint)PublicWeenieDesc.BitfieldIndex.BF_STUCK) != 0)
                                boolsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeBool.STUCK_BOOL}, {true})" + Environment.NewLine;
                            if (((uint)parsed.wdesc._bitfield & (uint)PublicWeenieDesc.BitfieldIndex.BF_UI_HIDDEN) != 0)
                                boolsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeBool.UI_HIDDEN_BOOL}, {true})" + Environment.NewLine;
                            if (((uint)parsed.wdesc._bitfield & (uint)PublicWeenieDesc.BitfieldIndex.BF_VENDOR) != 0)
                                boolsLine += $"     , ({parsed.wdesc._wcid}, {(uint)STypeBool.VENDOR_SERVICE_BOOL}, {true})" + Environment.NewLine;
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
