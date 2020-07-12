using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using ACE.Common.Extensions;
using ACE.Database.Models.World;
using ACE.Entity.Enum.Properties;
using aclogview.Properties;
using aclogview.SQLWriters;

namespace aclogview
{
    public partial class ExportACEWorldToolForm : Form
    {
        public ExportACEWorldToolForm()
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
                filesToProcess.AddRange(Directory.GetFiles(txtSearchPathRoot.Text, "*.csv", SearchOption.AllDirectories));

                txtSearchPathRoot.Enabled = false;
                btnChangeSearchPathRoot.Enabled = false;
                chkCompressOutput.Enabled = false;
                chkIncludeFullPathAndFileName.Enabled = false;
                btnStopBuild.Enabled = true;

                timer1.Start();

				ThreadPool.QueueUserWorkItem((state) =>
                {
                    // Do the actual work here
                    DoBuild();

                    if (!Disposing && !IsDisposed)
                        btnStopBuild.BeginInvoke((Action)(() => btnStopBuild_Click(null, null)));
                });
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

        private readonly FragDatListFile aceWorldFragDatFile = new FragDatListFile();

        private void DoBuild()
        {
            DateTime start = DateTime.Now;

            aceWorldFragDatFile.CreateFile(Path.Combine(txtOutputFolder.Text, "ACE-World.frags"), chkCompressOutput.Checked ? FragDatListFile.CompressionType.DeflateStream : FragDatListFile.CompressionType.None);

            // Do not parallel this search
            foreach (var currentFile in filesToProcess)
            {
                if (searchAborted || Disposing || IsDisposed)
                    break;

                try
                {
                    var fileStart = DateTime.Now;
                    System.Diagnostics.Debug.WriteLine($"Processing file {currentFile}");
                    if (Path.GetExtension(currentFile) == ".csv")
                        ProcessCSVFileForBuild(currentFile);
                    else
                        ProcessFileForBuild(currentFile);
                    System.Diagnostics.Debug.WriteLine($"File process started at {fileStart.ToString()}, completed at {DateTime.Now.ToString()} and took {(DateTime.Now - fileStart).TotalMinutes} minutes.");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("File failed to process with exception: " + Environment.NewLine + ex, "Exception", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            aceWorldFragDatFile.CloseFile();
            MessageBox.Show($"Build started at {start.ToString()}, completed at {DateTime.Now.ToString()} and took {(DateTime.Now - start).TotalMinutes} minutes.");
        }

        private void ProcessFileForBuild(string fileName)
        {
            // NOTE: If you want to get fully constructed/merged messages instead of fragments:
            // Pass true below and use record.data as the full message, instead of individual record.frags
            var isPcapng = false;
            var records = PCapReader.LoadPcap(fileName, true, ref searchAborted, ref isPcapng);

            // Temperorary objects
            var aceWorldFrags = new List<FragDatListFile.FragDatInfo>();

            foreach (var record in records)
            {
                if (searchAborted || Disposing || IsDisposed)
                    return;

                try
                {
                    Interlocked.Increment(ref fragmentsProcessed);

                    FragDatListFile.PacketDirection packetDirection = (record.isSend ? FragDatListFile.PacketDirection.ClientToServer : FragDatListFile.PacketDirection.ServerToClient);

                    BinaryReader fragDataReader = new BinaryReader(new MemoryStream(record.data));

                    var messageCode = fragDataReader.ReadUInt32();

                    uint opCode = 0;

                    if (messageCode == (uint)PacketOpcode.WEENIE_ORDERED_EVENT || messageCode == (uint)PacketOpcode.ORDERED_EVENT)
                    {
                        if (messageCode == (uint)PacketOpcode.WEENIE_ORDERED_EVENT)
                        {
                            WOrderHdr orderHeader = WOrderHdr.read(fragDataReader);
                            opCode = fragDataReader.ReadUInt32();
                        }
                        else if (messageCode == (uint)PacketOpcode.ORDERED_EVENT)
                        {
                            OrderHdr orderHeader = OrderHdr.read(fragDataReader);
                            opCode = fragDataReader.ReadUInt32();
                        }
                    }
                    else
                    {
                        opCode = messageCode;
                    }

                    if (opCode == (uint)PacketOpcode.Evt_Physics__CreateObject_ID
                        || opCode == (uint)PacketOpcode.APPRAISAL_INFO_EVENT
                        || opCode == (uint)PacketOpcode.BOOK_DATA_RESPONSE_EVENT
                        || opCode == (uint)PacketOpcode.BOOK_PAGE_DATA_RESPONSE_EVENT
                        || opCode == (uint)PacketOpcode.Evt_Writing__BookData_ID
                        || opCode == (uint)PacketOpcode.Evt_Writing__BookPageData_ID
                        || opCode == (uint)PacketOpcode.VENDOR_INFO_EVENT
                        || opCode == (uint)PacketOpcode.Evt_House__Recv_HouseProfile_ID
                        || opCode == (uint)PacketOpcode.Evt_House__Recv_HouseData_ID
                        || opCode == (uint)PacketOpcode.Evt_Login__WorldInfo_ID
                        )
                    {
                        Interlocked.Increment(ref totalHits);

                        aceWorldFrags.Add(new FragDatListFile.FragDatInfo(packetDirection, record.index, record.data));
                    }
                }
                catch
                {
                    // Do something with the exception maybe
                    Interlocked.Increment(ref totalExceptions);
                }
            }

            string outputFileName = (chkIncludeFullPathAndFileName.Checked ? fileName : (Path.GetFileName(fileName)));

            aceWorldFragDatFile.Write(new KeyValuePair<string, IList<FragDatListFile.FragDatInfo>>(outputFileName, aceWorldFrags));

            Interlocked.Increment(ref filesProcessed);
        }

        private void ProcessCSVFileForBuild(string fileName)
        {
            var records = File.ReadAllLines(fileName);

            // "Timestamp","LandCell","RawCoordinates","JSON"
            if (records.Length < 2 || records[0] != "\"Timestamp\",\"LandCell\",\"RawCoordinates\",\"JSON\"")
            {
                Interlocked.Increment(ref filesProcessed);
                return;
            }

            // Temperorary objects
            var aceWorldFrags = new List<FragDatListFile.FragDatInfo>();

            var jsonSerializer = new JavaScriptSerializer();

            var lineAppend = "";

            foreach (var record in records)
            {
                if (searchAborted || Disposing || IsDisposed)
                    return;

                var rawDataHdr = "{\"RawData\":\"";
                var idDataHdr = "{\"Id\":\"";

                var rawDataStringTrimmed = "";

                byte[] data = null;

                if (record.Contains(idDataHdr) || lineAppend != "")
                {
                    var rawDataHdrPos = record.IndexOf(idDataHdr);
                    var rawDataString = lineAppend == "" ? record.Substring(rawDataHdrPos) : record;

                    if (!rawDataString.EndsWith("}\""))
                    {
                        lineAppend += rawDataString;
                        continue;
                    }
                    else
                    {
                        rawDataString = lineAppend + rawDataString;
                        lineAppend = "";
                    }

                    rawDataStringTrimmed = rawDataString.Substring(0, rawDataString.Length - 1);

                    var stringsStart = ",\"StringValues\":{";
                    var stringsStartPos = rawDataStringTrimmed.IndexOf(stringsStart);
                    var justStrings = rawDataStringTrimmed.Substring(stringsStartPos);
                    var stringsEnd = "},\"";
                    var stringsEndPos = justStrings.IndexOf(stringsEnd);
                    justStrings = justStrings.Substring(0, stringsEndPos + stringsEnd.Length);

                    //Console.WriteLine(justStrings);

                    justStrings = justStrings.TrimStart(stringsStart);
                    justStrings = justStrings.TrimEnd(stringsEnd);

                    //Console.WriteLine(justStrings);

                    var sep1 = "\":\"";
                    var sep2 = "\",\"";

                    var justStringsTrimmed = justStrings.Replace(sep1,"{SEP1}");
                    justStringsTrimmed = justStringsTrimmed.Replace(sep2, "{SEP2}");

                    //Console.WriteLine(justStringsTrimmed);
                    justStringsTrimmed = justStringsTrimmed.Substring(1, justStringsTrimmed.Length - 1);
                    justStringsTrimmed = justStringsTrimmed.Substring(0, justStringsTrimmed.Length - 1);

                    //Console.WriteLine(justStringsTrimmed);

                    var justStringsClean = CleanForJSON(justStringsTrimmed);
                    //Console.WriteLine(justStringsClean);
                    justStringsClean = justStringsClean.Replace("{SEP1}", sep1);
                    justStringsClean = justStringsClean.Replace("{SEP2}", sep2);
                    //Console.WriteLine(justStringsClean);
                    justStringsClean = "\"" + justStringsClean + "\"";
                    //Console.WriteLine(justStringsClean);

                    var strings1 = stringsStart + justStrings + stringsEnd;
                    var strings2 = stringsStart + justStringsClean + stringsEnd;

                    //Console.WriteLine(rawDataStringTrimmed);
                    var stringsReplaced = rawDataStringTrimmed.Replace(strings1, strings2);
                    //Console.WriteLine(stringsReplaced);
                    rawDataStringTrimmed = stringsReplaced;

                    var idData = (Dictionary<string, object>)jsonSerializer.DeserializeObject(rawDataStringTrimmed);

                    var idpackethdr = new WOrderHdr();
                    idpackethdr.id = 1342814975u;
                    idpackethdr.stamp = 1u;

                    var setAppraiseInfo = new CM_Examine.SetAppraiseInfo();
                    setAppraiseInfo.i_prof = new CM_Examine.AppraisalProfile();

                    foreach (var kvp in idData)
                    {
                        switch (kvp.Key)
                        {
                            case "Id":
                                setAppraiseInfo.i_objid = (uint)int.Parse((string)kvp.Value);
                                setAppraiseInfo.i_prof.success_flag = 1;
                                break;

                            //case "ObjectClass":
                            //    ObjectClass = (ObjectClass)Enum.Parse(typeof(ObjectClass), (string)kvp.Value);
                            //    break;

                            case "BoolValues":
                                {
                                    var values = (Dictionary<string, object>)kvp.Value;

                                    foreach (var kvp2 in values)
                                    {
                                        var key = int.Parse(kvp2.Key);
                                        var value = bool.Parse(kvp2.Value.ToString());
                                        //var value = int.Parse(kvp2.Value.ToString());

                                        if (!Enum.IsDefined(typeof(STypeBool), key))
                                            continue;

                                        //BoolValues[key] = value;
                                        setAppraiseInfo.i_prof._boolStatsTable.hashTable.Add((STypeBool)key, Convert.ToInt32(value));

                                        setAppraiseInfo.i_prof.header |= (uint)CM_Examine.AppraisalProfile.AppraisalProfilePackHeader.Packed_BoolStats;
                                    }

                                    break;
                                }

                            case "DoubleValues":
                                {
                                    var values = (Dictionary<string, object>)kvp.Value;

                                    foreach (var kvp2 in values)
                                    {
                                        var key = int.Parse(kvp2.Key);
                                        var value = double.Parse(kvp2.Value.ToString());

                                        if (!Enum.IsDefined(typeof(STypeFloat), key))
                                            continue;

                                        //DoubleValues[key] = value;
                                        //setAppraiseInfo.i_prof._floatStatsTable.hashTable.Add((STypeFloat)key, value);

                                        //setAppraiseInfo.i_prof.header |= (uint)CM_Examine.AppraisalProfile.AppraisalProfilePackHeader.Packed_FloatStats;
                                    }

                                    break;
                                }

                            case "LongValues":
                                {
                                    var values = (Dictionary<string, object>)kvp.Value;

                                    foreach (var kvp2 in values)
                                    {
                                        //var key = (IntValueKey)int.Parse(kvp2.Key);
                                        var key = int.Parse(kvp2.Key);
                                        var value = int.Parse(kvp2.Value.ToString());

                                        if (!Enum.IsDefined(typeof(STypeInt64), key))
                                            continue;

                                        //LongValues[key] = value;
                                        //setAppraiseInfo.i_prof._int64StatsTable.hashTable.Add((STypeInt64)key, value);

                                        //setAppraiseInfo.i_prof.header |= (uint)CM_Examine.AppraisalProfile.AppraisalProfilePackHeader.Packed_Int64Stats;
                                    }

                                    break;
                                }

                            case "StringValues":
                                {
                                    var values = (Dictionary<string, object>)kvp.Value;

                                    foreach (var kvp2 in values)
                                    {
                                        var key = int.Parse(kvp2.Key);

                                        var pStringChar = new PStringChar();
                                        pStringChar.m_buffer = kvp2.Value.ToString();

                                        if (!Enum.IsDefined(typeof(STypeString), key))
                                            continue;

                                        //StringValues[key] = kvp2.Value.ToString();
                                        setAppraiseInfo.i_prof._strStatsTable.hashTable.Add((STypeString)key, pStringChar);

                                        setAppraiseInfo.i_prof.header |= (uint)CM_Examine.AppraisalProfile.AppraisalProfilePackHeader.Packed_StringStats;
                                    }

                                    break;
                                }

                                //case "ActiveSpells":
                                //    if (!string.IsNullOrEmpty((string)kvp.Value))
                                //    {
                                //        var spellsSplit = ((string)kvp.Value).Split(',');

                                //        foreach (var spell in spellsSplit)
                                //            ActiveSpells.Add(int.Parse(spell));
                                //    }

                                //    break;

                                //case "Spells":
                                //    if (!string.IsNullOrEmpty((string)kvp.Value))
                                //    {
                                //        var spellsSplit = ((string)kvp.Value).Split(',');

                                //        foreach (var spell in spellsSplit)
                                //            Spells.Add(int.Parse(spell));
                                //    }

                                //    break;

                                //case "Attributes":
                                //    {
                                //        ExtendIDAttributeInfo = new ExtendIDAttributeInfo();

                                //        var values = (Dictionary<string, object>)kvp.Value;

                                //        foreach (var kvp2 in values)
                                //        {
                                //            switch (kvp2.Key)
                                //            {
                                //                case "healthMax":
                                //                    ExtendIDAttributeInfo.healthMax = uint.Parse((string)kvp2.Value);
                                //                    break;

                                //                case "manaMax":
                                //                    ExtendIDAttributeInfo.manaMax = uint.Parse((string)kvp2.Value);
                                //                    break;

                                //                case "staminaMax":
                                //                    ExtendIDAttributeInfo.staminaMax = uint.Parse((string)kvp2.Value);
                                //                    break;

                                //                case "strength":
                                //                    ExtendIDAttributeInfo.strength = uint.Parse((string)kvp2.Value);
                                //                    break;

                                //                case "endurance":
                                //                    ExtendIDAttributeInfo.endurance = uint.Parse((string)kvp2.Value);
                                //                    break;

                                //                case "quickness":
                                //                    ExtendIDAttributeInfo.quickness = uint.Parse((string)kvp2.Value);
                                //                    break;

                                //                case "coordination":
                                //                    ExtendIDAttributeInfo.coordination = uint.Parse((string)kvp2.Value);
                                //                    break;

                                //                case "focus":
                                //                    ExtendIDAttributeInfo.focus = uint.Parse((string)kvp2.Value);
                                //                    break;

                                //                case "self":
                                //                    ExtendIDAttributeInfo.self = uint.Parse((string)kvp2.Value);
                                //                    break;

                                //                default:
                                //                    throw new NotImplementedException();
                                //            }
                                //        }

                                //        break;
                                //    }

                                //case "Resources":
                                //    {
                                //        var values = (Dictionary<string, object>)kvp.Value;

                                //        foreach (var kvp2 in values)
                                //        {
                                //            var key = int.Parse(kvp2.Key);
                                //            var value = int.Parse(kvp2.Value.ToString());

                                //            Resources[key] = value;
                                //        }

                                //        break;
                                //    }

                                //default:
                                //    throw new NotImplementedException();
                        }
                    }

                    using (MemoryStream stream = new MemoryStream())
                    {
                        using (BinaryWriter writer = new BinaryWriter(stream))
                        {
                            writer.Write((uint)PacketOpcode.WEENIE_ORDERED_EVENT);

                            writer.Write(idpackethdr.id);
                            writer.Write(idpackethdr.stamp);

                            writer.Write((uint)PacketOpcode.APPRAISAL_INFO_EVENT);
                            writer.Write(setAppraiseInfo.i_objid);
                            writer.Write(setAppraiseInfo.i_prof.header);
                            writer.Write(setAppraiseInfo.i_prof.success_flag);

                            //if (info.Flags.HasFlag(IdentifyResponseFlags.IntStatsTable))
                            //    writer.Write(info.PropertiesInt);
                            if ((setAppraiseInfo.i_prof.header & (uint)CM_Examine.AppraisalProfile.AppraisalProfilePackHeader.Packed_IntStats) != 0)
                            {
                                //writer.Write(info.PropertiesInt);
                                var properties = new SortedDictionary<STypeInt, int>(setAppraiseInfo.i_prof._intStatsTable.hashTable);

                                //PHashTable.WriteHeader(writer, properties.Count);
                                // uint uint uint - packedSize - write: (buckets) | (count & 0xFFFFFF)
                                // uint - buckets - read: 1 << (packedSize >> 24)
                                // uint - count - read: packedSize & 0xFFFFFF
                                //return (uint)Math.Log(num, 2) + 1;
                                //var bucketShift = GetNumBits((uint)count)/* - 1*/;
                                var count = properties.Count();
                                var bucketShift = (uint)Math.Log((uint)count, 2) + 1;/* - 1*/;
                                //var maxSize = 1 << ((int)bucketShift - 1);
                                var packedSize = (bucketShift << 24) | ((uint)count & 0xFFFFFF);
                                //var packedSize = ((uint)maxSize << 24) | ((uint)count & 0xFFFFFF);
                                writer.Write(packedSize);
                                foreach (var kvp in properties)
                                {
                                    writer.Write((uint)kvp.Key);
                                    writer.Write(kvp.Value);
                                }
                            }
                            //if (info.Flags.HasFlag(IdentifyResponseFlags.Int64StatsTable))
                            //    writer.Write(info.PropertiesInt64);
                            if ((setAppraiseInfo.i_prof.header & (uint)CM_Examine.AppraisalProfile.AppraisalProfilePackHeader.Packed_Int64Stats) != 0)
                            {
                                var properties = new SortedDictionary<STypeInt64, long>(setAppraiseInfo.i_prof._int64StatsTable.hashTable);

                                var count = properties.Count();
                                var bucketShift = (uint)Math.Log((uint)count, 2) + 1;/* - 1*/;
                                var packedSize = (bucketShift << 24) | ((uint)count & 0xFFFFFF);
                                writer.Write(packedSize);
                                foreach (var kvp in properties)
                                {
                                    writer.Write((uint)kvp.Key);
                                    writer.Write(kvp.Value);
                                }
                            }
                            //if (info.Flags.HasFlag(IdentifyResponseFlags.BoolStatsTable))
                            //    writer.Write(info.PropertiesBool);
                            if ((setAppraiseInfo.i_prof.header & (uint)CM_Examine.AppraisalProfile.AppraisalProfilePackHeader.Packed_BoolStats) != 0)
                            {
                                var properties = new SortedDictionary<STypeBool, int>(setAppraiseInfo.i_prof._boolStatsTable.hashTable);

                                var count = properties.Count();
                                var bucketShift = (uint)Math.Log((uint)count, 2) + 1;/* - 1*/;
                                var packedSize = (bucketShift << 24) | ((uint)count & 0xFFFFFF);
                                writer.Write(packedSize);
                                foreach (var kvp in properties)
                                {
                                    writer.Write((uint)kvp.Key);
                                    writer.Write(kvp.Value);
                                }
                            }
                            //if (info.Flags.HasFlag(IdentifyResponseFlags.FloatStatsTable))
                            //    writer.Write(info.PropertiesFloat);
                            if ((setAppraiseInfo.i_prof.header & (uint)CM_Examine.AppraisalProfile.AppraisalProfilePackHeader.Packed_FloatStats) != 0)
                            {
                                var properties = new SortedDictionary<STypeFloat, double>(setAppraiseInfo.i_prof._floatStatsTable.hashTable);

                                var count = properties.Count();
                                var bucketShift = (uint)Math.Log((uint)count, 2) + 1;/* - 1*/;
                                var packedSize = (bucketShift << 24) | ((uint)count & 0xFFFFFF);
                                writer.Write(packedSize);
                                foreach (var kvp in properties)
                                {
                                    writer.Write((uint)kvp.Key);
                                    writer.Write(kvp.Value);
                                }
                            }
                            //if (info.Flags.HasFlag(IdentifyResponseFlags.StringStatsTable))
                            //    writer.Write(info.PropertiesString);
                            if ((setAppraiseInfo.i_prof.header & (uint)CM_Examine.AppraisalProfile.AppraisalProfilePackHeader.Packed_StringStats) != 0)
                            {
                                var properties = new SortedDictionary<STypeString, PStringChar>(setAppraiseInfo.i_prof._strStatsTable.hashTable);

                                var count = properties.Count();
                                var bucketShift = (uint)Math.Log((uint)count, 2) + 1;/* - 1*/;
                                var packedSize = (bucketShift << 24) | ((uint)count & 0xFFFFFF);
                                writer.Write(packedSize);
                                foreach (var kvp in properties)
                                {
                                    writer.Write((uint)kvp.Key);
                                    //writer.Write(kvp.Value);

                                    var strValue = kvp.Value.m_buffer;

                                    if (strValue == null) strValue = "";

                                    writer.Write((ushort)strValue.Length);
                                    writer.Write(System.Text.Encoding.GetEncoding(1252).GetBytes(strValue));

                                    //CalculatePadMultiple(sizeof(ushort) + (uint)data.Length, 4u)
                                    //private static uint CalculatePadMultiple(uint length, uint multiple) { return multiple * ((length + multiple - 1u) / multiple) - length; }
                                    var length = sizeof(ushort) + (uint)strValue.Length;
                                    var multiple = 4u;

                                    writer.Write(new byte[multiple * ((length + multiple - 1u) / multiple) - length]);
                                }
                            }
                            //if (info.Flags.HasFlag(IdentifyResponseFlags.DidStatsTable))
                            //    writer.Write(info.PropertiesDID);
                            //if (info.Flags.HasFlag(IdentifyResponseFlags.SpellBook))
                            //    writer.Write(info.SpellBook);
                            //if (info.Flags.HasFlag(IdentifyResponseFlags.ArmorProfile))
                            //    writer.Write(info.ArmorProfile);
                            //if (info.Flags.HasFlag(IdentifyResponseFlags.CreatureProfile))
                            //    writer.Write(info.CreatureProfile);
                            //if (info.Flags.HasFlag(IdentifyResponseFlags.WeaponProfile))
                            //    writer.Write(info.WeaponProfile);
                            //if (info.Flags.HasFlag(IdentifyResponseFlags.HookProfile))
                            //    writer.Write(info.HookProfile);
                            //if (info.Flags.HasFlag(IdentifyResponseFlags.ArmorEnchantmentBitfield))
                            //{
                            //    writer.Write((ushort)info.ArmorHighlight);
                            //    writer.Write((ushort)info.ArmorColor);
                            //}
                            //if (info.Flags.HasFlag(IdentifyResponseFlags.WeaponEnchantmentBitfield))
                            //{
                            //    writer.Write((ushort)info.WeaponHighlight);
                            //    writer.Write((ushort)info.WeaponColor);
                            //}
                            //if (info.Flags.HasFlag(IdentifyResponseFlags.ResistEnchantmentBitfield))
                            //{
                            //    writer.Write((ushort)info.ResistHighlight);
                            //    writer.Write((ushort)info.ResistColor);
                            //}
                            //if (info.Flags.HasFlag(IdentifyResponseFlags.ArmorLevels))
                            //    writer.Write(info.ArmorLevels);
                        }
                        stream.Flush();
                        byte[] bytes = stream.GetBuffer();
                        //use it

                        data = bytes;
                    }
                }

                if (!record.Contains(rawDataHdr) && data == null)
                    continue;

                if (rawDataStringTrimmed == "")
                {
                    var rawDataHdrPos = record.IndexOf(rawDataHdr);
                    var rawDataString = record.Substring(rawDataHdrPos);

                    rawDataStringTrimmed = rawDataString.Substring(0, rawDataString.Length - 1);
                }

                Dictionary<string, object> result = (Dictionary<string, object>)jsonSerializer.DeserializeObject(rawDataStringTrimmed);

                if (result.Count != 1  && data == null)
                    continue;

                try
                {
                    Interlocked.Increment(ref fragmentsProcessed);

                    FragDatListFile.PacketDirection packetDirection = FragDatListFile.PacketDirection.ServerToClient;

                    if (data == null)
                        data = HexStringToByteArray((string)result.Values.FirstOrDefault());

                    BinaryReader fragDataReader = new BinaryReader(new MemoryStream(data));

                    var messageCode = fragDataReader.ReadUInt32();

                    uint opCode = 0;

                    if (messageCode == (uint)PacketOpcode.WEENIE_ORDERED_EVENT || messageCode == (uint)PacketOpcode.ORDERED_EVENT)
                    {
                        if (messageCode == (uint)PacketOpcode.WEENIE_ORDERED_EVENT)
                        {
                            WOrderHdr orderHeader = WOrderHdr.read(fragDataReader);
                            opCode = fragDataReader.ReadUInt32();
                        }
                        else if (messageCode == (uint)PacketOpcode.ORDERED_EVENT)
                        {
                            OrderHdr orderHeader = OrderHdr.read(fragDataReader);
                            opCode = fragDataReader.ReadUInt32();
                        }
                    }
                    else
                    {
                        opCode = messageCode;
                    }

                    if (opCode == (uint)PacketOpcode.Evt_Physics__CreateObject_ID
                        || opCode == (uint)PacketOpcode.APPRAISAL_INFO_EVENT
                        || opCode == (uint)PacketOpcode.BOOK_DATA_RESPONSE_EVENT
                        || opCode == (uint)PacketOpcode.BOOK_PAGE_DATA_RESPONSE_EVENT
                        || opCode == (uint)PacketOpcode.Evt_Writing__BookData_ID
                        || opCode == (uint)PacketOpcode.Evt_Writing__BookPageData_ID
                        || opCode == (uint)PacketOpcode.VENDOR_INFO_EVENT
                        || opCode == (uint)PacketOpcode.Evt_House__Recv_HouseProfile_ID
                        || opCode == (uint)PacketOpcode.Evt_House__Recv_HouseData_ID
                        || opCode == (uint)PacketOpcode.Evt_Login__WorldInfo_ID
                        )
                    {
                        Interlocked.Increment(ref totalHits);

                        aceWorldFrags.Add(new FragDatListFile.FragDatInfo(packetDirection, 1, data));
                    }
                }
                catch
                {
                    // Do something with the exception maybe
                    Interlocked.Increment(ref totalExceptions);
                }
            }

            string outputFileName = (chkIncludeFullPathAndFileName.Checked ? fileName : (Path.GetFileName(fileName)));

            aceWorldFragDatFile.Write(new KeyValuePair<string, IList<FragDatListFile.FragDatInfo>>(outputFileName, aceWorldFrags));

            Interlocked.Increment(ref filesProcessed);
        }

        public static byte[] HexStringToByteArray(string hex)
        {
            byte[] bytes = new byte[hex.Length / 2];

            for (int i = 0; i < hex.Length; i += 2)
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);

            return bytes;
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

                ThreadPool.QueueUserWorkItem((state) =>
                {
                    // Do the actual work here
                    DoProcess();

                    if (!Disposing && !IsDisposed)
                        btnStopProcess.BeginInvoke((Action)(() => btnStopProcess_Click(null, null)));
                });
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
                    //ProcessFileForExamination(currentFile);
                    ProcessFileForExport(currentFile);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("File failed to process with exception: " + Environment.NewLine + ex, "Exception", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        class Landblock
        {
            //public uint LandblockId;

            public Dictionary<uint, ACE.Database.Models.World.LandblockInstance> StaticObjects;

            public Dictionary<uint, ACE.Database.Models.World.LandblockInstance> LinkableMonsterObjects;

            public Dictionary<uint, ACE.Database.Models.World.LandblockInstance> LinkableItemObjects;

            public Dictionary<uint, ACE.Database.Models.World.LandblockInstance> LinkableNPCObjects;

            public Landblock()
            {
                StaticObjects = new Dictionary<uint, ACE.Database.Models.World.LandblockInstance>();
                LinkableMonsterObjects = new Dictionary<uint, ACE.Database.Models.World.LandblockInstance>();
                LinkableItemObjects = new Dictionary<uint, ACE.Database.Models.World.LandblockInstance>();
                LinkableNPCObjects = new Dictionary<uint, ACE.Database.Models.World.LandblockInstance>();
            }
        }

        private void ProcessFileForExport(string fileName)
        {
            var fragDatListFile = new FragDatListFile();
            DateTime start = DateTime.Now;

            if (!fragDatListFile.OpenFile(fileName))
                return;

            try
            {
                var exportTime = new DateTime(2019, 2, 10, 00, 00, 00);

                Dictionary<uint, ACE.Database.Models.World.Weenie> weenies = new Dictionary<uint, ACE.Database.Models.World.Weenie>();
                Dictionary<uint, uint> weeniesByGUID = new Dictionary<uint, uint>();
                Dictionary<uint, string> weenieNames = new Dictionary<uint, string>();

                //Dictionary<uint, ACE.Database.Models.World.LandblockInstance> instances = new Dictionary<uint, ACE.Database.Models.World.LandblockInstance>();

                Dictionary<uint, Landblock> instances = new Dictionary<uint, Landblock>();

                Dictionary<uint, List<Position>> processedWeeniePositions = new Dictionary<uint, List<Position>>();

                string currentWorld = "Unknown";
                Dictionary<string, Dictionary<uint, uint>> worldIDQueue = new Dictionary<string, Dictionary<uint, uint>>();
                //Dictionary<uint, uint> idObjectsStatus = new Dictionary<uint, uint>();
                //Dictionary<uint, uint> weenieIdObjectsStatus = new Dictionary<uint, uint>();
                Dictionary<uint, KeyValuePair<uint, uint>> weenieIdObjectsStatus = new Dictionary<uint, KeyValuePair<uint, uint>>();

                Dictionary<string, Dictionary<uint, string>> worldCorpseInstances = new Dictionary<string, Dictionary<uint, string>>();

                Dictionary<uint, CM_Physics.PublicWeenieDesc> vendorPWDs = new Dictionary<uint, CM_Physics.PublicWeenieDesc>();
                Dictionary<int, CM_Physics.CreateObject> itemTemplates = new Dictionary<int, CM_Physics.CreateObject>();

                worldIDQueue.Add(currentWorld, new Dictionary<uint, uint>());

                worldCorpseInstances.Add(currentWorld, new Dictionary<uint, string>());

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
                            if (frag.Data.Length <= 4)
                                continue;

                            BinaryReader fragDataReader = new BinaryReader(new MemoryStream(frag.Data));

                            var messageCode = fragDataReader.ReadUInt32();

                            uint opCode = 0;

                            if (messageCode == (uint)PacketOpcode.WEENIE_ORDERED_EVENT || messageCode == (uint)PacketOpcode.ORDERED_EVENT)
                            {
                                if (messageCode == (uint)PacketOpcode.WEENIE_ORDERED_EVENT)
                                {
                                    WOrderHdr orderHeader = WOrderHdr.read(fragDataReader);
                                    opCode = fragDataReader.ReadUInt32();
                                }
                                else if (messageCode == (uint)PacketOpcode.ORDERED_EVENT)
                                {
                                    OrderHdr orderHeader = OrderHdr.read(fragDataReader);
                                    opCode = fragDataReader.ReadUInt32();
                                }
                            }
                            else
                            {
                                opCode = messageCode;
                            }

                            if (opCode == (uint)PacketOpcode.Evt_Login__WorldInfo_ID)
                            {
                                var parsed = CM_Login.WorldInfo.read(fragDataReader);

                                currentWorld = parsed.strWorldName.m_buffer;

                                if (!worldIDQueue.ContainsKey(currentWorld))
                                    worldIDQueue.Add(currentWorld, new Dictionary<uint, uint>());

                                if (!worldCorpseInstances.ContainsKey(currentWorld))
                                    worldCorpseInstances.Add(currentWorld, new Dictionary<uint, string>());
                            }

                            if (opCode == (uint)PacketOpcode.Evt_Physics__CreateObject_ID)
                            {
                                var parsed = CM_Physics.CreateObject.read(fragDataReader);

                                if (!worldIDQueue[currentWorld].ContainsKey(parsed.object_id))
                                {
                                    bool addIt = true;
                                    if (parsed.wdesc._wcid == 1)
                                        if (worldIDQueue[currentWorld].ContainsValue(1))
                                            addIt = false;

                                    if (addIt)
                                        worldIDQueue[currentWorld].Add(parsed.object_id, parsed.wdesc._wcid);

                                    if (parsed.wdesc._wcid == 21)
                                    {
                                        if (!worldCorpseInstances[currentWorld].ContainsKey(parsed.object_id))
                                            worldCorpseInstances[currentWorld].Add(parsed.object_id, parsed.wdesc._name.m_buffer);
                                    }
                                }
                                else
                                {
                                    if (worldIDQueue[currentWorld][parsed.object_id] != parsed.wdesc._wcid)
                                    {
                                        worldIDQueue[currentWorld][parsed.object_id] = parsed.wdesc._wcid;
                                    }
                                }

                                //CreateStaticObjectsList(parsed,
                                //    objectIds, staticObjects,
                                //    outputAsLandblocks, landblockInstances,
                                //    weenieIds, weenies,
                                //    processedWeeniePositions, dedupeWeenies,
                                //    weenieNames,
                                //    appraisalObjectsCatagoryMap, appraisalObjectToWeenieId,
                                //    weenieObjectsCatagoryMap,
                                //    weeniesWeenieType, staticObjectsWeenieType,
                                //    wieldedObjectsParentMap, wieldedObjects,
                                //    inventoryParents, inventoryObjects,
                                //    parentWieldsWeenies,
                                //    weeniesTypeTemplate,
                                //    exportEverything,
                                //    corpseObjectsDroppedItems, corpseObjectsInstances,
                                //    chestObjectsContainedItems, chestObjectsInstances);

                                //if ((parsed.physicsdesc.pos.objcell_id >> 16) == 0x8603 || parsed.physicsdesc.pos.objcell_id == 0)
                                //if ((parsed.physicsdesc.pos.objcell_id >> 16) == 0x8602
                                //    || (parsed.physicsdesc.pos.objcell_id >> 16) == 0x8603
                                //    || (parsed.physicsdesc.pos.objcell_id >> 16) == 0x8604
                                //    || (parsed.physicsdesc.pos.objcell_id >> 16) == 0x8702
                                //    || (parsed.physicsdesc.pos.objcell_id >> 16) == 0x8703
                                //    || (parsed.physicsdesc.pos.objcell_id >> 16) == 0x7F03
                                //    || (parsed.physicsdesc.pos.objcell_id >> 16) == 0x7F04
                                //    || (parsed.physicsdesc.pos.objcell_id >> 16) == 0x8002
                                //    || (parsed.physicsdesc.pos.objcell_id >> 16) == 0x8003
                                //    || (parsed.physicsdesc.pos.objcell_id >> 16) == 0x8004
                                //    || (parsed.physicsdesc.pos.objcell_id >> 16) == 0x8C04
                                //    || (parsed.physicsdesc.pos.objcell_id >> 16) == 0x8D02
                                //    || (parsed.physicsdesc.pos.objcell_id >> 16) == 0x8D03
                                //    || (parsed.physicsdesc.pos.objcell_id >> 16) == 0x8D04
                                //    || (parsed.physicsdesc.pos.objcell_id >> 16) == 0x8E02
                                //    || (parsed.physicsdesc.pos.objcell_id >> 16) == 0x7202
                                //    || (parsed.physicsdesc.pos.objcell_id >> 16) == 0x7203
                                //    || (parsed.physicsdesc.pos.objcell_id >> 16) == 0x7204
                                //    || (parsed.physicsdesc.pos.objcell_id >> 16) == 0x7302
                                //    || (parsed.physicsdesc.pos.objcell_id >> 16) == 0x7303
                                //    )
                                //if (/*(parsed.physicsdesc.pos.objcell_id >> 16) == 0x8602*/
                                //    (parsed.physicsdesc.pos.objcell_id >> 16) == 0x8603
                                //    /* || (parsed.physicsdesc.pos.objcell_id >> 16) == 0x8604
                                //    || (parsed.physicsdesc.pos.objcell_id >> 16) == 0x8702
                                //    || (parsed.physicsdesc.pos.objcell_id >> 16) == 0x8703 */
                                //    || (parsed.physicsdesc.pos.objcell_id >> 16) == 0x7F03
                                //    /* || (parsed.physicsdesc.pos.objcell_id >> 16) == 0x7F04
                                //    || (parsed.physicsdesc.pos.objcell_id >> 16) == 0x8002
                                //    || (parsed.physicsdesc.pos.objcell_id >> 16) == 0x8003
                                //    || (parsed.physicsdesc.pos.objcell_id >> 16) == 0x8004 */
                                //    || (parsed.physicsdesc.pos.objcell_id >> 16) == 0x8C04
                                //    /* || (parsed.physicsdesc.pos.objcell_id >> 16) == 0x8D02
                                //    || (parsed.physicsdesc.pos.objcell_id >> 16) == 0x8D03
                                //    || (parsed.physicsdesc.pos.objcell_id >> 16) == 0x8D04
                                //    || (parsed.physicsdesc.pos.objcell_id >> 16) == 0x8E02
                                //    || (parsed.physicsdesc.pos.objcell_id >> 16) == 0x7202 */
                                //    || (parsed.physicsdesc.pos.objcell_id >> 16) == 0x7203
                                //    /* || (parsed.physicsdesc.pos.objcell_id >> 16) == 0x7204
                                //    || (parsed.physicsdesc.pos.objcell_id >> 16) == 0x7302
                                //    || (parsed.physicsdesc.pos.objcell_id >> 16) == 0x7303 */
                                //    || parsed.physicsdesc.pos.objcell_id == 0
                                //    )

                                //if ((parsed.physicsdesc.pos.objcell_id >> 16) == 0x00AF || (parsed.physicsdesc.pos.objcell_id >> 16) == 0x00B0 || (parsed.physicsdesc.pos.objcell_id >> 16) == 0x00B6)// || parsed.physicsdesc.pos.objcell_id == 0)
                                //if ((parsed.physicsdesc.pos.objcell_id >> 16) == 0x76E9 || (parsed.physicsdesc.pos.objcell_id >> 16) == 0x77E7 || parsed.physicsdesc.pos.objcell_id == 0)
                                //if ((parsed.physicsdesc.pos.objcell_id >> 16) == 0x01EF || (parsed.physicsdesc.pos.objcell_id >> 16) == 0x0188 || parsed.physicsdesc.pos.objcell_id == 0)
                                //if ((parsed.physicsdesc.pos.objcell_id >> 16) == 0xAE71 || (parsed.physicsdesc.pos.objcell_id >> 16) == 0x00AF || (((parsed.physicsdesc.pos.objcell_id >> 16) >= 0x00B0) && (parsed.physicsdesc.pos.objcell_id >> 16) <= 0x00B6) || parsed.physicsdesc.pos.objcell_id == 0)
                                //if ((parsed.physicsdesc.pos.objcell_id >> 16) == 0xA2A1 || (parsed.physicsdesc.pos.objcell_id >> 16) == 0x9FA6 || (parsed.physicsdesc.pos.objcell_id >> 16) == 0xA4A7)
                                //{

                                    if (!weenies.ContainsKey(parsed.wdesc._wcid))
                                    {
                                        var wo = CreateWeenieFromCreateObjectMsg(parsed);

                                        SetWeenieType(wo);

                                        if (!itemTemplates.ContainsKey(wo.Type))
                                            itemTemplates.Add(wo.Type, parsed);

                                        weenies.Add(parsed.wdesc._wcid, wo);

                                        if (!weenieNames.ContainsKey(parsed.wdesc._wcid))
                                            weenieNames.Add(parsed.wdesc._wcid, parsed.wdesc._name.m_buffer);

                                        if (!processedWeeniePositions.ContainsKey(parsed.wdesc._wcid))
                                            processedWeeniePositions.Add(parsed.wdesc._wcid, new List<Position>());

                                        totalHits++;

                                        if (wo.Type == (int)ACE.Entity.Enum.WeenieType.Hook)
                                        {
                                            var remove = false;
                                            var name = wo.GetProperty(ACE.Entity.Enum.Properties.PropertyString.Name);
                                            var type = (ACE.Entity.Enum.HookType)wo.GetProperty(ACE.Entity.Enum.Properties.PropertyInt.HookType);

                                            if (name != (Enum.GetName(typeof(ACE.Entity.Enum.HookType), type) + " Hook"))
                                                remove = true;

                                            if (remove)
                                            {
                                                weenies.Remove(wo.ClassId);
                                                weenieNames.Remove(wo.ClassId);
                                            }
                                        }
                                    }

                                    if (!weeniesByGUID.ContainsKey(parsed.object_id))
                                    {
                                        weeniesByGUID.Add(parsed.object_id, parsed.wdesc._wcid);
                                        totalHits++;
                                    }

                                    if (parsed.wdesc._wcid != 1 && parsed.wdesc._containerID == 0 && parsed.wdesc._wielderID == 0) // Skip players and objects without
                                    {
                                        if (parsed.object_id > 0x5FFFFFFF && parsed.object_id < 0x80000000) // static objects
                                        {
                                            //if (!instances.ContainsKey(parsed.object_id))
                                            //{
                                            //    //instances.Add(parsed.object_id,
                                            //    //    new ACE.Database.Models.World.LandblockInstance
                                            //    //    {
                                            //    //        Guid = parsed.object_id,
                                            //    //        WeenieClassId = parsed.wdesc._wcid,
                                            //    //        ObjCellId = parsed.physicsdesc.pos.objcell_id,
                                            //    //        OriginX = parsed.physicsdesc.pos.frame.m_fOrigin.x,
                                            //    //        OriginY = parsed.physicsdesc.pos.frame.m_fOrigin.y,
                                            //    //        OriginZ = parsed.physicsdesc.pos.frame.m_fOrigin.z,
                                            //    //        AnglesW = parsed.physicsdesc.pos.frame.qw,
                                            //    //        AnglesX = parsed.physicsdesc.pos.frame.qx,
                                            //    //        AnglesY = parsed.physicsdesc.pos.frame.qy,
                                            //    //        AnglesZ = parsed.physicsdesc.pos.frame.qz,
                                            //    //        IsLinkChild = false
                                            //    //    });

                                            //    totalHits++;
                                            //}
                                            if (parsed.physicsdesc.pos.objcell_id > 0)
                                            {
                                                var landblockId = parsed.physicsdesc.pos.objcell_id >> 16;
                                                if (!instances.ContainsKey(landblockId))
                                                {
                                                    instances.Add(landblockId, new Landblock());
                                                }

                                                if (!instances[landblockId].StaticObjects.ContainsKey(parsed.object_id))
                                                {
                                                    instances[landblockId].StaticObjects.Add(parsed.object_id,
                                                        new ACE.Database.Models.World.LandblockInstance
                                                        {
                                                            Guid = parsed.object_id,
                                                            WeenieClassId = parsed.wdesc._wcid,
                                                            ObjCellId = parsed.physicsdesc.pos.objcell_id,
                                                            OriginX = parsed.physicsdesc.pos.frame.m_fOrigin.x,
                                                            OriginY = parsed.physicsdesc.pos.frame.m_fOrigin.y,
                                                            OriginZ = parsed.physicsdesc.pos.frame.m_fOrigin.z,
                                                            AnglesW = parsed.physicsdesc.pos.frame.qw,
                                                            AnglesX = parsed.physicsdesc.pos.frame.qx,
                                                            AnglesY = parsed.physicsdesc.pos.frame.qy,
                                                            AnglesZ = parsed.physicsdesc.pos.frame.qz,
                                                            IsLinkChild = false
                                                        });
                                                }
                                            }
                                        }
                                        else
                                        {
                                            if (parsed.physicsdesc.pos.objcell_id > 0 && parsed.wdesc._wielderID == 0)
                                            {
                                                var landblockId = parsed.physicsdesc.pos.objcell_id >> 16;
                                                if (!instances.ContainsKey(landblockId))
                                                {
                                                    instances.Add(landblockId, new Landblock());
                                                }

                                                float margin = 0.02f;

                                                if (weenies[parsed.wdesc._wcid].Type == (int)ACE.Entity.Enum.WeenieType.Creature && parsed.wdesc._blipColor != (int)ACE.Entity.Enum.RadarColor.NPC)
                                                {
                                                    //margin = 0.05f;
                                                    margin = 2f;
                                                    if (!instances[landblockId].LinkableMonsterObjects.ContainsKey(parsed.object_id)
                                                        && !PositionRecorded(parsed, processedWeeniePositions[parsed.wdesc._wcid], parsed.physicsdesc.pos, margin))
                                                    {
                                                        instances[landblockId].LinkableMonsterObjects.Add(parsed.object_id,
                                                            new ACE.Database.Models.World.LandblockInstance
                                                            {
                                                                Guid = parsed.object_id,
                                                                WeenieClassId = parsed.wdesc._wcid,
                                                                ObjCellId = parsed.physicsdesc.pos.objcell_id,
                                                                OriginX = parsed.physicsdesc.pos.frame.m_fOrigin.x,
                                                                OriginY = parsed.physicsdesc.pos.frame.m_fOrigin.y,
                                                                OriginZ = parsed.physicsdesc.pos.frame.m_fOrigin.z,
                                                                AnglesW = parsed.physicsdesc.pos.frame.qw,
                                                                AnglesX = parsed.physicsdesc.pos.frame.qx,
                                                                AnglesY = parsed.physicsdesc.pos.frame.qy,
                                                                AnglesZ = parsed.physicsdesc.pos.frame.qz,
                                                                IsLinkChild = false
                                                            });

                                                        processedWeeniePositions[parsed.wdesc._wcid].Add(parsed.physicsdesc.pos);
                                                    }
                                                }
                                                else if (weenies[parsed.wdesc._wcid].Type == (int)ACE.Entity.Enum.WeenieType.Creature && parsed.wdesc._blipColor == (int)ACE.Entity.Enum.RadarColor.NPC)
                                                {
                                                    if (!instances[landblockId].LinkableNPCObjects.ContainsKey(parsed.object_id)
                                                        && !PositionRecorded(parsed, processedWeeniePositions[parsed.wdesc._wcid], parsed.physicsdesc.pos, margin))
                                                    {
                                                        instances[landblockId].LinkableNPCObjects.Add(parsed.object_id,
                                                            new ACE.Database.Models.World.LandblockInstance
                                                            {
                                                                Guid = parsed.object_id,
                                                                WeenieClassId = parsed.wdesc._wcid,
                                                                ObjCellId = parsed.physicsdesc.pos.objcell_id,
                                                                OriginX = parsed.physicsdesc.pos.frame.m_fOrigin.x,
                                                                OriginY = parsed.physicsdesc.pos.frame.m_fOrigin.y,
                                                                OriginZ = parsed.physicsdesc.pos.frame.m_fOrigin.z,
                                                                AnglesW = parsed.physicsdesc.pos.frame.qw,
                                                                AnglesX = parsed.physicsdesc.pos.frame.qx,
                                                                AnglesY = parsed.physicsdesc.pos.frame.qy,
                                                                AnglesZ = parsed.physicsdesc.pos.frame.qz,
                                                                IsLinkChild = false
                                                            });

                                                        processedWeeniePositions[parsed.wdesc._wcid].Add(parsed.physicsdesc.pos);
                                                    }
                                                }
                                                else if (weenies[parsed.wdesc._wcid].Type != (int)ACE.Entity.Enum.WeenieType.Missile
                                                    && weenies[parsed.wdesc._wcid].Type != (int)ACE.Entity.Enum.WeenieType.Coin
                                                    && weenies[parsed.wdesc._wcid].Type != (int)ACE.Entity.Enum.WeenieType.Corpse
                                                    && weenies[parsed.wdesc._wcid].Type != (int)ACE.Entity.Enum.WeenieType.ProjectileSpell
                                                    && weenies[parsed.wdesc._wcid].Type != (int)ACE.Entity.Enum.WeenieType.Pet
                                                    && weenies[parsed.wdesc._wcid].Type != (int)ACE.Entity.Enum.WeenieType.PetDevice
                                                    && weenies[parsed.wdesc._wcid].Type != (int)ACE.Entity.Enum.WeenieType.CombatPet
                                                    && weenies[parsed.wdesc._wcid].Type != (int)ACE.Entity.Enum.WeenieType.Ammunition
                                                    )
                                                {
                                                    margin = 2.0f;
                                                    if (!instances[landblockId].LinkableItemObjects.ContainsKey(parsed.object_id)
                                                        && !PositionRecorded(parsed, processedWeeniePositions[parsed.wdesc._wcid], parsed.physicsdesc.pos, margin))
                                                    {
                                                        instances[landblockId].LinkableItemObjects.Add(parsed.object_id,
                                                            new ACE.Database.Models.World.LandblockInstance
                                                            {
                                                                Guid = parsed.object_id,
                                                                WeenieClassId = parsed.wdesc._wcid,
                                                                ObjCellId = parsed.physicsdesc.pos.objcell_id,
                                                                OriginX = parsed.physicsdesc.pos.frame.m_fOrigin.x,
                                                                OriginY = parsed.physicsdesc.pos.frame.m_fOrigin.y,
                                                                OriginZ = parsed.physicsdesc.pos.frame.m_fOrigin.z,
                                                                AnglesW = parsed.physicsdesc.pos.frame.qw,
                                                                AnglesX = parsed.physicsdesc.pos.frame.qx,
                                                                AnglesY = parsed.physicsdesc.pos.frame.qy,
                                                                AnglesZ = parsed.physicsdesc.pos.frame.qz,
                                                                IsLinkChild = false
                                                            });

                                                        processedWeeniePositions[parsed.wdesc._wcid].Add(parsed.physicsdesc.pos);
                                                    }
                                                }
                                            }
                                        }
                                    }

                                    if (parsed.wdesc._wielderID > 0)
                                    {
                                        if (worldIDQueue[currentWorld].ContainsKey(parsed.wdesc._wielderID))
                                        {
                                            if (weenies.ContainsKey(worldIDQueue[currentWorld][parsed.wdesc._wielderID]))
                                            {
                                                if (!weenies[worldIDQueue[currentWorld][parsed.wdesc._wielderID]].WeeniePropertiesCreateList.Any(y => y.WeenieClassId == parsed.wdesc._wcid))
                                                    weenies[worldIDQueue[currentWorld][parsed.wdesc._wielderID]].WeeniePropertiesCreateList.Add(new WeeniePropertiesCreateList
                                                    {
                                                        DestinationType = (int)ACE.Entity.Enum.DestinationType.Wield,
                                                        WeenieClassId = parsed.wdesc._wcid,
                                                        StackSize = 1,
                                                        Palette = 0,
                                                        Shade = 0,
                                                        TryToBond = false
                                                    });
                                            }
                                        }
                                    }

                                    if (parsed.wdesc._containerID > 0)
                                    {
                                        if (worldIDQueue[currentWorld].ContainsKey(parsed.wdesc._containerID))
                                        {
                                            if (weenies.ContainsKey(worldIDQueue[currentWorld][parsed.wdesc._containerID]))
                                            {
                                                if (!weenies[worldIDQueue[currentWorld][parsed.wdesc._containerID]].WeeniePropertiesGenerator.Any(y => y.WeenieClassId == parsed.wdesc._wcid)
                                                    && weenies[worldIDQueue[currentWorld][parsed.wdesc._containerID]].Type == (int)ACE.Entity.Enum.WeenieType.Chest)
                                                    weenies[worldIDQueue[currentWorld][parsed.wdesc._containerID]].WeeniePropertiesGenerator.Add(new WeeniePropertiesGenerator
                                                    {
                                                        Probability = -1,
                                                        WeenieClassId = parsed.wdesc._wcid,
                                                        StackSize = 1,
                                                        PaletteId = 0,
                                                        Shade = 0,
                                                        Delay = 0,
                                                        InitCreate = 1,
                                                        MaxCreate = 1,
                                                        WhenCreate = (int)ACE.Entity.Enum.RegenerationType.PickUp,
                                                        WhereCreate = (int)ACE.Entity.Enum.RegenLocationType.Contain
                                                    });
                                                else if (weenies[worldIDQueue[currentWorld][parsed.wdesc._containerID]].Type == (int)ACE.Entity.Enum.WeenieType.Corpse)
                                                {
                                                    bool corpse = false;
                                                    bool treasure = false;

                                                    if (worldCorpseInstances[currentWorld].ContainsKey(parsed.wdesc._containerID))
                                                    {
                                                        if (worldCorpseInstances[currentWorld][parsed.wdesc._containerID].StartsWith("Corpse of"))
                                                            corpse = true;

                                                        if (worldCorpseInstances[currentWorld][parsed.wdesc._containerID].StartsWith("Treasure of"))
                                                            treasure = true;

                                                        string nameToMatch = "";
                                                        if (corpse)
                                                            nameToMatch = worldCorpseInstances[currentWorld][parsed.wdesc._containerID].Replace("Corpse of ", "");
                                                        if (treasure)
                                                            nameToMatch = worldCorpseInstances[currentWorld][parsed.wdesc._containerID].Replace("Treasure of ", "");

                                                        if (weenieNames.Values.Contains(nameToMatch))
                                                        {
                                                            var wcid = weenieNames.Where(i => i.Value == nameToMatch).FirstOrDefault().Key;
                                                            if (weenies.ContainsKey(wcid))
                                                            {
                                                                if (!weenies[wcid].WeeniePropertiesCreateList.Any(y => y.WeenieClassId == parsed.wdesc._wcid))
                                                                    weenies[wcid].WeeniePropertiesCreateList.Add(new WeeniePropertiesCreateList
                                                                    {
                                                                        DestinationType = (int)ACE.Entity.Enum.DestinationType.ContainTreasure,
                                                                        WeenieClassId = parsed.wdesc._wcid,
                                                                        StackSize = (parsed.wdesc._stackSize > 0) ? parsed.wdesc._stackSize : 0,
                                                                        Palette = 0,
                                                                        Shade = 0,
                                                                        TryToBond = false
                                                                    });

                                                                if (treasure && !weenies[wcid].WeeniePropertiesBool.Any(y => y.Type == (ushort)ACE.Entity.Enum.Properties.PropertyBool.TreasureCorpse))
                                                                    weenies[wcid].WeeniePropertiesBool.Add(new ACE.Database.Models.World.WeeniePropertiesBool { Type = (ushort)ACE.Entity.Enum.Properties.PropertyBool.TreasureCorpse, Value = true });
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }

                                //}

                            }

                            if (opCode == (uint)PacketOpcode.APPRAISAL_INFO_EVENT)
                            {
                                var parsed = CM_Examine.SetAppraiseInfo.read(fragDataReader);

                                //CreateAppraisalObjectsList(parsed,
                                //    objectIds, staticObjects,
                                //    weenieIds, weenies,
                                //    appraisalObjects, appraisalObjectIds, appraisalObjectsCatagoryMap, appraisalObjectToWeenieId,
                                //    weenieAppraisalObjects,
                                //    weenieAppraisalObjectsIdx, weenieAppraisalObjectsSuccess,
                                //    weenieObjectsCatagoryMap,
                                //    currentWorld, worldIDQueue, idObjectsStatus, weenieIdObjectsStatus);


                                bool addIt = true;
                                if (worldIDQueue[currentWorld].ContainsKey(parsed.i_objid))
                                {

                                    if (weenies.ContainsKey(worldIDQueue[currentWorld][parsed.i_objid]))
                                    {

                                        if (weenieIdObjectsStatus.ContainsKey(worldIDQueue[currentWorld][parsed.i_objid]))
                                        {
                                            var success = weenieIdObjectsStatus[worldIDQueue[currentWorld][parsed.i_objid]].Key;
                                            var header = weenieIdObjectsStatus[worldIDQueue[currentWorld][parsed.i_objid]].Value;
                                            if (success == 0 && parsed.i_prof.success_flag == 1 && parsed.i_prof.header >= header)
                                            {
                                                weenieIdObjectsStatus.Remove(worldIDQueue[currentWorld][parsed.i_objid]);
                                            }
                                            else
                                                addIt = false;
                                        }

                                        if (weenies.ContainsKey(worldIDQueue[currentWorld][parsed.i_objid]))
                                        {
                                            if (weenies[worldIDQueue[currentWorld][parsed.i_objid]].Type == (int)ACE.Entity.Enum.WeenieType.ProjectileSpell)
                                                addIt = false;
                                        }

                                        if (addIt)
                                        {
                                            foreach (var x in parsed.i_prof._intStatsTable.hashTable)
                                            {
                                                if (!weenies[worldIDQueue[currentWorld][parsed.i_objid]].WeeniePropertiesInt.Any(y => y.Type == (ushort)x.Key))
                                                    weenies[worldIDQueue[currentWorld][parsed.i_objid]].WeeniePropertiesInt.Add(new ACE.Database.Models.World.WeeniePropertiesInt { Type = (ushort)x.Key, Value = x.Value });
                                            }
                                            foreach (var x in parsed.i_prof._int64StatsTable.hashTable)
                                            {
                                                if (!weenies[worldIDQueue[currentWorld][parsed.i_objid]].WeeniePropertiesInt64.Any(y => y.Type == (ushort)x.Key))
                                                    weenies[worldIDQueue[currentWorld][parsed.i_objid]].WeeniePropertiesInt64.Add(new ACE.Database.Models.World.WeeniePropertiesInt64 { Type = (ushort)x.Key, Value = x.Value });
                                            }
                                            foreach (var x in parsed.i_prof._boolStatsTable.hashTable)
                                            {
                                                if (!weenies[worldIDQueue[currentWorld][parsed.i_objid]].WeeniePropertiesBool.Any(y => y.Type == (ushort)x.Key))
                                                    weenies[worldIDQueue[currentWorld][parsed.i_objid]].WeeniePropertiesBool.Add(new ACE.Database.Models.World.WeeniePropertiesBool { Type = (ushort)x.Key, Value = x.Value == 1 });
                                            }
                                            foreach (var x in parsed.i_prof._floatStatsTable.hashTable)
                                            {
                                                if (!weenies[worldIDQueue[currentWorld][parsed.i_objid]].WeeniePropertiesFloat.Any(y => y.Type == (ushort)x.Key))
                                                    weenies[worldIDQueue[currentWorld][parsed.i_objid]].WeeniePropertiesFloat.Add(new ACE.Database.Models.World.WeeniePropertiesFloat { Type = (ushort)x.Key, Value = x.Value });
                                            }
                                            foreach (var x in parsed.i_prof._strStatsTable.hashTable)
                                            {
                                                if (!weenies[worldIDQueue[currentWorld][parsed.i_objid]].WeeniePropertiesString.Any(y => y.Type == (ushort)x.Key))
                                                    weenies[worldIDQueue[currentWorld][parsed.i_objid]].WeeniePropertiesString.Add(new ACE.Database.Models.World.WeeniePropertiesString { Type = (ushort)x.Key, Value = x.Value.m_buffer });
                                            }
                                            foreach (var x in parsed.i_prof._didStatsTable.hashTable)
                                            {
                                                if (!weenies[worldIDQueue[currentWorld][parsed.i_objid]].WeeniePropertiesDID.Any(y => y.Type == (ushort)x.Key))
                                                    weenies[worldIDQueue[currentWorld][parsed.i_objid]].WeeniePropertiesDID.Add(new ACE.Database.Models.World.WeeniePropertiesDID { Type = (ushort)x.Key, Value = x.Value });
                                            }
                                            foreach (var x in parsed.i_prof._spellsTable.list)
                                            {
                                                if ((int)x < 0)
                                                    continue;
                                                if (!weenies[worldIDQueue[currentWorld][parsed.i_objid]].WeeniePropertiesSpellBook.Any(y => y.Spell == (int)x))
                                                    weenies[worldIDQueue[currentWorld][parsed.i_objid]].WeeniePropertiesSpellBook.Add(new ACE.Database.Models.World.WeeniePropertiesSpellBook { Spell = (int)x, Probability = 2f });
                                            }

                                            if ((parsed.i_prof.header & (uint)CM_Examine.AppraisalProfile.AppraisalProfilePackHeader.Packed_ArmorProfile) != 0)
                                            {
                                                if (!weenies[worldIDQueue[currentWorld][parsed.i_objid]].WeeniePropertiesFloat.Any(y => y.Type == (ushort)STypeFloat.ARMOR_MOD_VS_SLASH_FLOAT))
                                                    weenies[worldIDQueue[currentWorld][parsed.i_objid]].WeeniePropertiesFloat.Add(new ACE.Database.Models.World.WeeniePropertiesFloat { Type = (ushort)STypeFloat.ARMOR_MOD_VS_SLASH_FLOAT, Value = parsed.i_prof._armorProfileTable._mod_vs_slash });
                                                if (!weenies[worldIDQueue[currentWorld][parsed.i_objid]].WeeniePropertiesFloat.Any(y => y.Type == (ushort)STypeFloat.ARMOR_MOD_VS_PIERCE_FLOAT))
                                                    weenies[worldIDQueue[currentWorld][parsed.i_objid]].WeeniePropertiesFloat.Add(new ACE.Database.Models.World.WeeniePropertiesFloat { Type = (ushort)STypeFloat.ARMOR_MOD_VS_PIERCE_FLOAT, Value = parsed.i_prof._armorProfileTable._mod_vs_pierce });
                                                if (!weenies[worldIDQueue[currentWorld][parsed.i_objid]].WeeniePropertiesFloat.Any(y => y.Type == (ushort)STypeFloat.ARMOR_MOD_VS_BLUDGEON_FLOAT))
                                                    weenies[worldIDQueue[currentWorld][parsed.i_objid]].WeeniePropertiesFloat.Add(new ACE.Database.Models.World.WeeniePropertiesFloat { Type = (ushort)STypeFloat.ARMOR_MOD_VS_BLUDGEON_FLOAT, Value = parsed.i_prof._armorProfileTable._mod_vs_bludgeon });
                                                if (!weenies[worldIDQueue[currentWorld][parsed.i_objid]].WeeniePropertiesFloat.Any(y => y.Type == (ushort)STypeFloat.ARMOR_MOD_VS_COLD_FLOAT))
                                                    weenies[worldIDQueue[currentWorld][parsed.i_objid]].WeeniePropertiesFloat.Add(new ACE.Database.Models.World.WeeniePropertiesFloat { Type = (ushort)STypeFloat.ARMOR_MOD_VS_COLD_FLOAT, Value = parsed.i_prof._armorProfileTable._mod_vs_cold });
                                                if (!weenies[worldIDQueue[currentWorld][parsed.i_objid]].WeeniePropertiesFloat.Any(y => y.Type == (ushort)STypeFloat.ARMOR_MOD_VS_FIRE_FLOAT))
                                                    weenies[worldIDQueue[currentWorld][parsed.i_objid]].WeeniePropertiesFloat.Add(new ACE.Database.Models.World.WeeniePropertiesFloat { Type = (ushort)STypeFloat.ARMOR_MOD_VS_FIRE_FLOAT, Value = parsed.i_prof._armorProfileTable._mod_vs_fire });
                                                if (!weenies[worldIDQueue[currentWorld][parsed.i_objid]].WeeniePropertiesFloat.Any(y => y.Type == (ushort)STypeFloat.ARMOR_MOD_VS_ACID_FLOAT))
                                                    weenies[worldIDQueue[currentWorld][parsed.i_objid]].WeeniePropertiesFloat.Add(new ACE.Database.Models.World.WeeniePropertiesFloat { Type = (ushort)STypeFloat.ARMOR_MOD_VS_ACID_FLOAT, Value = parsed.i_prof._armorProfileTable._mod_vs_acid });
                                                if (!weenies[worldIDQueue[currentWorld][parsed.i_objid]].WeeniePropertiesFloat.Any(y => y.Type == (ushort)STypeFloat.ARMOR_MOD_VS_NETHER_FLOAT))
                                                    weenies[worldIDQueue[currentWorld][parsed.i_objid]].WeeniePropertiesFloat.Add(new ACE.Database.Models.World.WeeniePropertiesFloat { Type = (ushort)STypeFloat.ARMOR_MOD_VS_NETHER_FLOAT, Value = parsed.i_prof._armorProfileTable._mod_vs_nether });
                                                if (!weenies[worldIDQueue[currentWorld][parsed.i_objid]].WeeniePropertiesFloat.Any(y => y.Type == (ushort)STypeFloat.ARMOR_MOD_VS_ELECTRIC_FLOAT))
                                                    weenies[worldIDQueue[currentWorld][parsed.i_objid]].WeeniePropertiesFloat.Add(new ACE.Database.Models.World.WeeniePropertiesFloat { Type = (ushort)STypeFloat.ARMOR_MOD_VS_ELECTRIC_FLOAT, Value = parsed.i_prof._armorProfileTable._mod_vs_electric });
                                            }

                                            if ((parsed.i_prof.header & (uint)CM_Examine.AppraisalProfile.AppraisalProfilePackHeader.Packed_CreatureProfile) != 0)
                                            {
                                                if ((parsed.i_prof._creatureProfileTable._header & (uint)CM_Examine.CreatureAppraisalProfile.CreatureAppraisalProfilePackHeader.Packed_Attributes) != 0)
                                                {
                                                    if (!weenies[worldIDQueue[currentWorld][parsed.i_objid]].WeeniePropertiesAttribute.Any(y => y.Type == (ushort)ACE.Entity.Enum.Properties.PropertyAttribute.Strength))
                                                        weenies[worldIDQueue[currentWorld][parsed.i_objid]].WeeniePropertiesAttribute.Add(new ACE.Database.Models.World.WeeniePropertiesAttribute { Type = (ushort)ACE.Entity.Enum.Properties.PropertyAttribute.Strength, InitLevel = parsed.i_prof._creatureProfileTable._strength });
                                                    if (!weenies[worldIDQueue[currentWorld][parsed.i_objid]].WeeniePropertiesAttribute.Any(y => y.Type == (ushort)ACE.Entity.Enum.Properties.PropertyAttribute.Endurance))
                                                        weenies[worldIDQueue[currentWorld][parsed.i_objid]].WeeniePropertiesAttribute.Add(new ACE.Database.Models.World.WeeniePropertiesAttribute { Type = (ushort)ACE.Entity.Enum.Properties.PropertyAttribute.Endurance, InitLevel = parsed.i_prof._creatureProfileTable._endurance });
                                                    if (!weenies[worldIDQueue[currentWorld][parsed.i_objid]].WeeniePropertiesAttribute.Any(y => y.Type == (ushort)ACE.Entity.Enum.Properties.PropertyAttribute.Quickness))
                                                        weenies[worldIDQueue[currentWorld][parsed.i_objid]].WeeniePropertiesAttribute.Add(new ACE.Database.Models.World.WeeniePropertiesAttribute { Type = (ushort)ACE.Entity.Enum.Properties.PropertyAttribute.Quickness, InitLevel = parsed.i_prof._creatureProfileTable._quickness });
                                                    if (!weenies[worldIDQueue[currentWorld][parsed.i_objid]].WeeniePropertiesAttribute.Any(y => y.Type == (ushort)ACE.Entity.Enum.Properties.PropertyAttribute.Coordination))
                                                        weenies[worldIDQueue[currentWorld][parsed.i_objid]].WeeniePropertiesAttribute.Add(new ACE.Database.Models.World.WeeniePropertiesAttribute { Type = (ushort)ACE.Entity.Enum.Properties.PropertyAttribute.Coordination, InitLevel = parsed.i_prof._creatureProfileTable._coordination });
                                                    if (!weenies[worldIDQueue[currentWorld][parsed.i_objid]].WeeniePropertiesAttribute.Any(y => y.Type == (ushort)ACE.Entity.Enum.Properties.PropertyAttribute.Focus))
                                                        weenies[worldIDQueue[currentWorld][parsed.i_objid]].WeeniePropertiesAttribute.Add(new ACE.Database.Models.World.WeeniePropertiesAttribute { Type = (ushort)ACE.Entity.Enum.Properties.PropertyAttribute.Focus, InitLevel = parsed.i_prof._creatureProfileTable._focus });
                                                    if (!weenies[worldIDQueue[currentWorld][parsed.i_objid]].WeeniePropertiesAttribute.Any(y => y.Type == (ushort)ACE.Entity.Enum.Properties.PropertyAttribute.Self))
                                                        weenies[worldIDQueue[currentWorld][parsed.i_objid]].WeeniePropertiesAttribute.Add(new ACE.Database.Models.World.WeeniePropertiesAttribute { Type = (ushort)ACE.Entity.Enum.Properties.PropertyAttribute.Self, InitLevel = parsed.i_prof._creatureProfileTable._self });

                                                    if (!weenies[worldIDQueue[currentWorld][parsed.i_objid]].WeeniePropertiesAttribute2nd.Any(y => y.Type == (ushort)ACE.Entity.Enum.Properties.PropertyAttribute2nd.MaxStamina))
                                                        weenies[worldIDQueue[currentWorld][parsed.i_objid]].WeeniePropertiesAttribute2nd.Add(new ACE.Database.Models.World.WeeniePropertiesAttribute2nd { Type = (ushort)ACE.Entity.Enum.Properties.PropertyAttribute2nd.MaxStamina, CurrentLevel = parsed.i_prof._creatureProfileTable._max_stamina, InitLevel = parsed.i_prof._creatureProfileTable._max_stamina - parsed.i_prof._creatureProfileTable._endurance });
                                                    if (!weenies[worldIDQueue[currentWorld][parsed.i_objid]].WeeniePropertiesAttribute2nd.Any(y => y.Type == (ushort)ACE.Entity.Enum.Properties.PropertyAttribute2nd.MaxMana))
                                                        weenies[worldIDQueue[currentWorld][parsed.i_objid]].WeeniePropertiesAttribute2nd.Add(new ACE.Database.Models.World.WeeniePropertiesAttribute2nd { Type = (ushort)ACE.Entity.Enum.Properties.PropertyAttribute2nd.MaxMana, CurrentLevel = parsed.i_prof._creatureProfileTable._max_mana, InitLevel = parsed.i_prof._creatureProfileTable._max_mana - parsed.i_prof._creatureProfileTable._self });
                                                }

                                                if (!weenies[worldIDQueue[currentWorld][parsed.i_objid]].WeeniePropertiesAttribute2nd.Any(y => y.Type == (ushort)ACE.Entity.Enum.Properties.PropertyAttribute2nd.MaxHealth))
                                                    if (parsed.i_prof._creatureProfileTable._endurance > 0)
                                                        weenies[worldIDQueue[currentWorld][parsed.i_objid]].WeeniePropertiesAttribute2nd.Add(new ACE.Database.Models.World.WeeniePropertiesAttribute2nd { Type = (ushort)ACE.Entity.Enum.Properties.PropertyAttribute2nd.MaxHealth, CurrentLevel = parsed.i_prof._creatureProfileTable._max_health, InitLevel = parsed.i_prof._creatureProfileTable._max_health - (parsed.i_prof._creatureProfileTable._endurance / 2) });
                                                    else
                                                        weenies[worldIDQueue[currentWorld][parsed.i_objid]].WeeniePropertiesAttribute2nd.Add(new ACE.Database.Models.World.WeeniePropertiesAttribute2nd { Type = (ushort)ACE.Entity.Enum.Properties.PropertyAttribute2nd.MaxHealth, CurrentLevel = parsed.i_prof._creatureProfileTable._max_health, InitLevel = 0 });
                                                else
                                                {
                                                    var health = weenies[worldIDQueue[currentWorld][parsed.i_objid]].WeeniePropertiesAttribute2nd.FirstOrDefault(y => y.Type == (ushort)ACE.Entity.Enum.Properties.PropertyAttribute2nd.MaxHealth);
                                                    if (health != null)
                                                    {
                                                        if (health.InitLevel == 0 && parsed.i_prof._creatureProfileTable._endurance > 0)
                                                            health.InitLevel = parsed.i_prof._creatureProfileTable._max_health - (parsed.i_prof._creatureProfileTable._endurance / 2);
                                                    }
                                                }
                                            }

                                            if ((parsed.i_prof.header & (uint)CM_Examine.AppraisalProfile.AppraisalProfilePackHeader.Packed_WeaponProfile) != 0)
                                            {
                                                if (!weenies[worldIDQueue[currentWorld][parsed.i_objid]].WeeniePropertiesInt.Any(y => y.Type == (ushort)STypeInt.DAMAGE_TYPE_INT))
                                                    weenies[worldIDQueue[currentWorld][parsed.i_objid]].WeeniePropertiesInt.Add(new ACE.Database.Models.World.WeeniePropertiesInt { Type = (ushort)STypeInt.DAMAGE_TYPE_INT, Value = (int)parsed.i_prof._weaponProfileTable._damage_type });
                                                if (!weenies[worldIDQueue[currentWorld][parsed.i_objid]].WeeniePropertiesInt.Any(y => y.Type == (ushort)STypeInt.WEAPON_TIME_INT))
                                                    weenies[worldIDQueue[currentWorld][parsed.i_objid]].WeeniePropertiesInt.Add(new ACE.Database.Models.World.WeeniePropertiesInt { Type = (ushort)STypeInt.WEAPON_TIME_INT, Value = (int)parsed.i_prof._weaponProfileTable._weapon_time });
                                                if (!weenies[worldIDQueue[currentWorld][parsed.i_objid]].WeeniePropertiesInt.Any(y => y.Type == (ushort)STypeInt.WEAPON_SKILL_INT))
                                                    weenies[worldIDQueue[currentWorld][parsed.i_objid]].WeeniePropertiesInt.Add(new ACE.Database.Models.World.WeeniePropertiesInt { Type = (ushort)STypeInt.WEAPON_SKILL_INT, Value = (int)parsed.i_prof._weaponProfileTable._weapon_skill });
                                                if (!weenies[worldIDQueue[currentWorld][parsed.i_objid]].WeeniePropertiesInt.Any(y => y.Type == (ushort)STypeInt.DAMAGE_INT))
                                                    weenies[worldIDQueue[currentWorld][parsed.i_objid]].WeeniePropertiesInt.Add(new ACE.Database.Models.World.WeeniePropertiesInt { Type = (ushort)STypeInt.DAMAGE_INT, Value = (int)parsed.i_prof._weaponProfileTable._weapon_damage });
                                                if (!weenies[worldIDQueue[currentWorld][parsed.i_objid]].WeeniePropertiesFloat.Any(y => y.Type == (ushort)STypeFloat.DAMAGE_VARIANCE_FLOAT))
                                                    weenies[worldIDQueue[currentWorld][parsed.i_objid]].WeeniePropertiesFloat.Add(new ACE.Database.Models.World.WeeniePropertiesFloat { Type = (ushort)STypeFloat.DAMAGE_VARIANCE_FLOAT, Value = parsed.i_prof._weaponProfileTable._damage_variance });
                                                if (!weenies[worldIDQueue[currentWorld][parsed.i_objid]].WeeniePropertiesFloat.Any(y => y.Type == (ushort)STypeFloat.DAMAGE_MOD_FLOAT))
                                                    weenies[worldIDQueue[currentWorld][parsed.i_objid]].WeeniePropertiesFloat.Add(new ACE.Database.Models.World.WeeniePropertiesFloat { Type = (ushort)STypeFloat.DAMAGE_MOD_FLOAT, Value = parsed.i_prof._weaponProfileTable._damage_mod });
                                                if (!weenies[worldIDQueue[currentWorld][parsed.i_objid]].WeeniePropertiesFloat.Any(y => y.Type == (ushort)STypeFloat.WEAPON_LENGTH_FLOAT))
                                                    weenies[worldIDQueue[currentWorld][parsed.i_objid]].WeeniePropertiesFloat.Add(new ACE.Database.Models.World.WeeniePropertiesFloat { Type = (ushort)STypeFloat.WEAPON_LENGTH_FLOAT, Value = parsed.i_prof._weaponProfileTable._weapon_length });
                                                if (!weenies[worldIDQueue[currentWorld][parsed.i_objid]].WeeniePropertiesFloat.Any(y => y.Type == (ushort)STypeFloat.MAXIMUM_VELOCITY_FLOAT))
                                                    weenies[worldIDQueue[currentWorld][parsed.i_objid]].WeeniePropertiesFloat.Add(new ACE.Database.Models.World.WeeniePropertiesFloat { Type = (ushort)STypeFloat.MAXIMUM_VELOCITY_FLOAT, Value = parsed.i_prof._weaponProfileTable._max_velocity });
                                                if (!weenies[worldIDQueue[currentWorld][parsed.i_objid]].WeeniePropertiesFloat.Any(y => y.Type == (ushort)STypeFloat.WEAPON_OFFENSE_FLOAT))
                                                    weenies[worldIDQueue[currentWorld][parsed.i_objid]].WeeniePropertiesFloat.Add(new ACE.Database.Models.World.WeeniePropertiesFloat { Type = (ushort)STypeFloat.WEAPON_OFFENSE_FLOAT, Value = parsed.i_prof._weaponProfileTable._weapon_offense });
                                                if (!weenies[worldIDQueue[currentWorld][parsed.i_objid]].WeeniePropertiesInt.Any(y => y.Type == 8030))
                                                    weenies[worldIDQueue[currentWorld][parsed.i_objid]].WeeniePropertiesInt.Add(new ACE.Database.Models.World.WeeniePropertiesInt { Type = 8030, Value = (int)parsed.i_prof._weaponProfileTable._max_velocity_estimated });
                                            }

                                            if ((parsed.i_prof.header & (uint)CM_Examine.AppraisalProfile.AppraisalProfilePackHeader.Packed_HookProfile) != 0)
                                            {
                                            }

                                            if ((parsed.i_prof.header & (uint)CM_Examine.AppraisalProfile.AppraisalProfilePackHeader.Packed_ArmorLevels) != 0)
                                            {
                                            }

                                            weenieIdObjectsStatus.Add(worldIDQueue[currentWorld][parsed.i_objid], new KeyValuePair<uint, uint>(parsed.i_prof.success_flag, parsed.i_prof.header));

                                            totalHits++;
                                        }
                                    }
                                }
                            }

                            if (opCode == (uint)PacketOpcode.BOOK_DATA_RESPONSE_EVENT)
                            {
                                var parsed = CM_Writing.BookDataResponse.read(fragDataReader);

                                //CreateBookObjectsList(parsed,
                                //    objectIds, staticObjects,
                                //    weenieIds, weenies,
                                //    appraisalObjects, appraisalObjectIds, appraisalObjectsCatagoryMap, appraisalObjectToWeenieId,
                                //    bookObjectIds, bookObjects,
                                //    weenieObjectsCatagoryMap,
                                //    currentWorld, worldIDQueue);

                                if (worldIDQueue[currentWorld].ContainsKey(parsed.i_bookID))
                                {

                                    if (weenies.ContainsKey(worldIDQueue[currentWorld][parsed.i_bookID]))
                                    {
                                        weenies[worldIDQueue[currentWorld][parsed.i_bookID]].WeeniePropertiesBook = new ACE.Database.Models.World.WeeniePropertiesBook();

                                        weenies[worldIDQueue[currentWorld][parsed.i_bookID]].WeeniePropertiesBook.MaxNumCharsPerPage = (int)parsed.maxNumCharsPerPage;
                                        weenies[worldIDQueue[currentWorld][parsed.i_bookID]].WeeniePropertiesBook.MaxNumPages = parsed.i_maxNumPages;

                                        if (!weenies[worldIDQueue[currentWorld][parsed.i_bookID]].WeeniePropertiesIID.Any(y => y.Type == (ushort)STypeIID.SCRIBE_IID) && parsed.authorId > 0)
                                            weenies[worldIDQueue[currentWorld][parsed.i_bookID]].WeeniePropertiesIID.Add(new ACE.Database.Models.World.WeeniePropertiesIID { Type = (ushort)STypeIID.SCRIBE_IID, Value = parsed.authorId });
                                        if (!weenies[worldIDQueue[currentWorld][parsed.i_bookID]].WeeniePropertiesString.Any(y => y.Type == (ushort)STypeString.SCRIBE_NAME_STRING) && parsed.authorName.m_buffer != null)
                                            weenies[worldIDQueue[currentWorld][parsed.i_bookID]].WeeniePropertiesString.Add(new ACE.Database.Models.World.WeeniePropertiesString { Type = (ushort)STypeString.SCRIBE_NAME_STRING, Value = parsed.authorName.m_buffer });
                                        if (!weenies[worldIDQueue[currentWorld][parsed.i_bookID]].WeeniePropertiesString.Any(y => y.Type == (ushort)STypeString.INSCRIPTION_STRING) && parsed.inscription.m_buffer != null)
                                            weenies[worldIDQueue[currentWorld][parsed.i_bookID]].WeeniePropertiesString.Add(new ACE.Database.Models.World.WeeniePropertiesString { Type = (ushort)STypeString.INSCRIPTION_STRING, Value = parsed.inscription.m_buffer });

                                        var i = 0;
                                        foreach (var page in parsed.pageData.list)
                                        {
                                            if (page.textIncluded == 0)
                                                continue;

                                            if (!weenies[worldIDQueue[currentWorld][parsed.i_bookID]].WeeniePropertiesBookPageData.Any(y => y.PageId == i))
                                            {
                                                weenies[worldIDQueue[currentWorld][parsed.i_bookID]].WeeniePropertiesBookPageData.Add(new ACE.Database.Models.World.WeeniePropertiesBookPageData { PageId = (uint)i, AuthorAccount = page.authorAccount.m_buffer, AuthorId = page.authorID, AuthorName = (page.authorName.m_buffer == null) ? "prewritten" : page.authorName.m_buffer, IgnoreAuthor = page.ignoreAuthor == 1, PageText = page.pageText.m_buffer });
                                            }
                                            i++;
                                        }

                                        totalHits++;
                                    }
                                }
                            }

                            if (opCode == (uint)PacketOpcode.BOOK_PAGE_DATA_RESPONSE_EVENT)
                            {
                                var parsed = CM_Writing.BookPageDataResponse.read(fragDataReader);

                                //CreatePageObjectsList(parsed,
                                //    objectIds, staticObjects,
                                //    weenieIds, weenies,
                                //    appraisalObjects, appraisalObjectIds, appraisalObjectsCatagoryMap, appraisalObjectToWeenieId,
                                //    pageObjectIds, pageObjects,
                                //    weenieObjectsCatagoryMap,
                                //    currentWorld, worldIDQueue);

                                if (worldIDQueue[currentWorld].ContainsKey(parsed.bookID))
                                {
                                    //weenies[weeniesByGUID[parsed.i_bookID].ClassId].WeeniePropertiesBook.MaxNumCharsPerPage = (int)parsed.maxNumCharsPerPage;
                                    //weenies[weeniesByGUID[parsed.i_bookID].ClassId].WeeniePropertiesBook.MaxNumPages = parsed.i_maxNumPages;

                                    //if (!weenies[weeniesByGUID[parsed.i_bookID].ClassId].WeeniePropertiesIID.Any(y => y.Type == (ushort)STypeIID.SCRIBE_IID))
                                    //    weenies[weeniesByGUID[parsed.i_bookID].ClassId].WeeniePropertiesIID.Add(new ACE.Database.Models.World.WeeniePropertiesIID { Type = (ushort)STypeIID.SCRIBE_IID, Value = parsed.authorId });
                                    //if (!weenies[weeniesByGUID[parsed.i_bookID].ClassId].WeeniePropertiesIID.Any(y => y.Type == (ushort)STypeString.SCRIBE_NAME_STRING))
                                    //    weenies[weeniesByGUID[parsed.i_bookID].ClassId].WeeniePropertiesString.Add(new ACE.Database.Models.World.WeeniePropertiesString { Type = (ushort)STypeString.SCRIBE_NAME_STRING, Value = parsed.authorName.m_buffer });
                                    //if (!weenies[weeniesByGUID[parsed.i_bookID].ClassId].WeeniePropertiesIID.Any(y => y.Type == (ushort)STypeString.INSCRIPTION_STRING))
                                    //    weenies[weeniesByGUID[parsed.i_bookID].ClassId].WeeniePropertiesString.Add(new ACE.Database.Models.World.WeeniePropertiesString { Type = (ushort)STypeString.INSCRIPTION_STRING, Value = parsed.inscription.m_buffer });

                                    if (weenies.ContainsKey(worldIDQueue[currentWorld][parsed.bookID]))
                                    {
                                        if (!weenies[worldIDQueue[currentWorld][parsed.bookID]].WeeniePropertiesBookPageData.Any(y => y.PageId == parsed.page))
                                        {
                                            weenies[worldIDQueue[currentWorld][parsed.bookID]].WeeniePropertiesBookPageData.Add(new ACE.Database.Models.World.WeeniePropertiesBookPageData { PageId = parsed.page, AuthorAccount = parsed.pageData.authorAccount.m_buffer, AuthorId = parsed.pageData.authorID, AuthorName = (parsed.pageData.authorName.m_buffer == null) ? "prewritten" : parsed.pageData.authorName.m_buffer, IgnoreAuthor = parsed.pageData.ignoreAuthor == 1, PageText = parsed.pageData.pageText.m_buffer });
                                        }

                                        totalHits++;
                                    }
                                }
                            }

                            if (opCode == (uint)PacketOpcode.VENDOR_INFO_EVENT)
                            {
                                var parsed = CM_Vendor.gmVendorUI.read(fragDataReader);

                                //CreateVendorObjectsList(parsed,
                                //    objectIds, staticObjects,
                                //    weenieIds, weenies,
                                //    appraisalObjects, appraisalObjectIds, appraisalObjectsCatagoryMap, appraisalObjectToWeenieId,
                                //    vendorObjectIds, vendorObjects, vendorSellsWeenies,
                                //    weeniesFromVendors,
                                //    weeniesTypeTemplate,
                                //    weenieObjectsCatagoryMap,
                                //    currentWorld, worldIDQueue);

                                if (worldIDQueue[currentWorld].ContainsKey(parsed.shopVendorID))
                                {
                                    if (weenies.ContainsKey(worldIDQueue[currentWorld][parsed.shopVendorID]))
                                    {
                                        if (!weenies[worldIDQueue[currentWorld][parsed.shopVendorID]].WeeniePropertiesFloat.Any(y => y.Type == (ushort)STypeFloat.BUY_PRICE_FLOAT))
                                            weenies[worldIDQueue[currentWorld][parsed.shopVendorID]].WeeniePropertiesFloat.Add(new ACE.Database.Models.World.WeeniePropertiesFloat { Type = (ushort)STypeFloat.BUY_PRICE_FLOAT, Value = parsed.shopVendorProfile.buy_price });
                                        if (!weenies[worldIDQueue[currentWorld][parsed.shopVendorID]].WeeniePropertiesFloat.Any(y => y.Type == (ushort)STypeFloat.SELL_PRICE_FLOAT))
                                            weenies[worldIDQueue[currentWorld][parsed.shopVendorID]].WeeniePropertiesFloat.Add(new ACE.Database.Models.World.WeeniePropertiesFloat { Type = (ushort)STypeFloat.SELL_PRICE_FLOAT, Value = parsed.shopVendorProfile.sell_price });
                                        if (!weenies[worldIDQueue[currentWorld][parsed.shopVendorID]].WeeniePropertiesInt.Any(y => y.Type == (ushort)STypeInt.MERCHANDISE_ITEM_TYPES_INT))
                                            weenies[worldIDQueue[currentWorld][parsed.shopVendorID]].WeeniePropertiesInt.Add(new ACE.Database.Models.World.WeeniePropertiesInt { Type = (ushort)STypeInt.MERCHANDISE_ITEM_TYPES_INT, Value = (int)parsed.shopVendorProfile.item_types });
                                        if (!weenies[worldIDQueue[currentWorld][parsed.shopVendorID]].WeeniePropertiesInt.Any(y => y.Type == (ushort)STypeInt.MERCHANDISE_MAX_VALUE_INT))
                                            weenies[worldIDQueue[currentWorld][parsed.shopVendorID]].WeeniePropertiesInt.Add(new ACE.Database.Models.World.WeeniePropertiesInt { Type = (ushort)STypeInt.MERCHANDISE_MAX_VALUE_INT, Value = (int)parsed.shopVendorProfile.max_value });
                                        if (!weenies[worldIDQueue[currentWorld][parsed.shopVendorID]].WeeniePropertiesInt.Any(y => y.Type == (ushort)STypeInt.MERCHANDISE_MIN_VALUE_INT))
                                            weenies[worldIDQueue[currentWorld][parsed.shopVendorID]].WeeniePropertiesInt.Add(new ACE.Database.Models.World.WeeniePropertiesInt { Type = (ushort)STypeInt.MERCHANDISE_MIN_VALUE_INT, Value = (int)parsed.shopVendorProfile.min_value });
                                        if (!weenies[worldIDQueue[currentWorld][parsed.shopVendorID]].WeeniePropertiesDID.Any(y => y.Type == (ushort)STypeDID.ALTERNATE_CURRENCY_DID) && parsed.shopVendorProfile.trade_id > 0)
                                            weenies[worldIDQueue[currentWorld][parsed.shopVendorID]].WeeniePropertiesDID.Add(new ACE.Database.Models.World.WeeniePropertiesDID { Type = (ushort)STypeDID.ALTERNATE_CURRENCY_DID, Value = parsed.shopVendorProfile.trade_id });
                                        if (!weenies[worldIDQueue[currentWorld][parsed.shopVendorID]].WeeniePropertiesBool.Any(y => y.Type == (ushort)STypeBool.DEAL_MAGICAL_ITEMS_BOOL))
                                            weenies[worldIDQueue[currentWorld][parsed.shopVendorID]].WeeniePropertiesBool.Add(new ACE.Database.Models.World.WeeniePropertiesBool { Type = (ushort)STypeBool.DEAL_MAGICAL_ITEMS_BOOL, Value = parsed.shopVendorProfile.magic == 1 });

                                        if (parsed.shopItemProfileList.list.Count > 0)
                                        {
                                            foreach (var item in parsed.shopItemProfileList.list)
                                            {
                                                if (!vendorPWDs.ContainsKey(item.pwd._wcid))
                                                    vendorPWDs.Add(item.pwd._wcid, item.pwd);

                                                //if (item.pwd._wcid > 31000)
                                                //    continue; // skip new things for now
                                                if (!weenies[worldIDQueue[currentWorld][parsed.shopVendorID]].WeeniePropertiesCreateList.Any(y => y.DestinationType == (sbyte)ACE.Entity.Enum.DestinationType.Shop && y.WeenieClassId == item.pwd._wcid))
                                                    weenies[worldIDQueue[currentWorld][parsed.shopVendorID]].WeeniePropertiesCreateList.Add(new ACE.Database.Models.World.WeeniePropertiesCreateList { DestinationType = (sbyte)ACE.Entity.Enum.DestinationType.Shop, WeenieClassId = item.pwd._wcid, StackSize = -1, TryToBond = false });
                                            }
                                        }

                                        totalHits++;
                                    }
                                }
                            }

                            if (opCode == (uint)PacketOpcode.Evt_House__Recv_HouseProfile_ID)
                            {
                                var parsed = CM_House.Recv_HouseProfile.read(fragDataReader);

                                //CreateSlumlordObjectsList(parsed,
                                //    objectIds, staticObjects,
                                //    weenieIds, weenies,
                                //    appraisalObjects, appraisalObjectIds, appraisalObjectsCatagoryMap, appraisalObjectToWeenieId,
                                //    slumlordObjectIds, slumlordObjects,
                                //    weeniesTypeTemplate,
                                //    weenieObjectsCatagoryMap,
                                //    currentWorld, worldIDQueue);

                                if (worldIDQueue[currentWorld].ContainsKey(parsed.lord))
                                {
                                    if (weenies.ContainsKey(worldIDQueue[currentWorld][parsed.lord]))
                                    {
                                        //if (!weenies[worldIDQueue[currentWorld][parsed.shopVendorID]].WeeniePropertiesFloat.Any(y => y.Type == (ushort)STypeFloat.BUY_PRICE_FLOAT))
                                        //    weenies[worldIDQueue[currentWorld][parsed.shopVendorID]].WeeniePropertiesFloat.Add(new ACE.Database.Models.World.WeeniePropertiesFloat { Type = (ushort)STypeFloat.BUY_PRICE_FLOAT, Value = parsed.shopVendorProfile.buy_price });
                                        //if (!weenies[worldIDQueue[currentWorld][parsed.shopVendorID]].WeeniePropertiesFloat.Any(y => y.Type == (ushort)STypeFloat.SELL_PRICE_FLOAT))
                                        //    weenies[worldIDQueue[currentWorld][parsed.shopVendorID]].WeeniePropertiesFloat.Add(new ACE.Database.Models.World.WeeniePropertiesFloat { Type = (ushort)STypeFloat.SELL_PRICE_FLOAT, Value = parsed.shopVendorProfile.sell_price });
                                        //if (!weenies[worldIDQueue[currentWorld][parsed.shopVendorID]].WeeniePropertiesInt.Any(y => y.Type == (ushort)STypeInt.MERCHANDISE_ITEM_TYPES_INT))
                                        //    weenies[worldIDQueue[currentWorld][parsed.shopVendorID]].WeeniePropertiesInt.Add(new ACE.Database.Models.World.WeeniePropertiesInt { Type = (ushort)STypeInt.MERCHANDISE_ITEM_TYPES_INT, Value = (int)parsed.shopVendorProfile.item_types });
                                        //if (!weenies[worldIDQueue[currentWorld][parsed.shopVendorID]].WeeniePropertiesInt.Any(y => y.Type == (ushort)STypeInt.MERCHANDISE_MAX_VALUE_INT))
                                        //    weenies[worldIDQueue[currentWorld][parsed.shopVendorID]].WeeniePropertiesInt.Add(new ACE.Database.Models.World.WeeniePropertiesInt { Type = (ushort)STypeInt.MERCHANDISE_MAX_VALUE_INT, Value = (int)parsed.shopVendorProfile.max_value });
                                        //if (!weenies[worldIDQueue[currentWorld][parsed.shopVendorID]].WeeniePropertiesInt.Any(y => y.Type == (ushort)STypeInt.MERCHANDISE_MIN_VALUE_INT))
                                        //    weenies[worldIDQueue[currentWorld][parsed.shopVendorID]].WeeniePropertiesInt.Add(new ACE.Database.Models.World.WeeniePropertiesInt { Type = (ushort)STypeInt.MERCHANDISE_MIN_VALUE_INT, Value = (int)parsed.shopVendorProfile.min_value });
                                        //if (!weenies[worldIDQueue[currentWorld][parsed.shopVendorID]].WeeniePropertiesDID.Any(y => y.Type == (ushort)STypeDID.ALTERNATE_CURRENCY_DID) && parsed.shopVendorProfile.trade_id > 0)
                                        //    weenies[worldIDQueue[currentWorld][parsed.shopVendorID]].WeeniePropertiesDID.Add(new ACE.Database.Models.World.WeeniePropertiesDID { Type = (ushort)STypeDID.ALTERNATE_CURRENCY_DID, Value = parsed.shopVendorProfile.trade_id });
                                        //if (!weenies[worldIDQueue[currentWorld][parsed.shopVendorID]].WeeniePropertiesBool.Any(y => y.Type == (ushort)STypeBool.DEAL_MAGICAL_ITEMS_BOOL))
                                        //    weenies[worldIDQueue[currentWorld][parsed.shopVendorID]].WeeniePropertiesBool.Add(new ACE.Database.Models.World.WeeniePropertiesBool { Type = (ushort)STypeBool.DEAL_MAGICAL_ITEMS_BOOL, Value = parsed.shopVendorProfile.magic == 1 });

                                        if (!weenies[worldIDQueue[currentWorld][parsed.lord]].WeeniePropertiesInt.Any(y => y.Type == (ushort)STypeInt.HOUSE_TYPE_INT))
                                            weenies[worldIDQueue[currentWorld][parsed.lord]].WeeniePropertiesInt.Add(new ACE.Database.Models.World.WeeniePropertiesInt { Type = (ushort)STypeInt.HOUSE_TYPE_INT, Value = (int)parsed.prof._type });
                                        if (!weenies[worldIDQueue[currentWorld][parsed.lord]].WeeniePropertiesInt.Any(y => y.Type == (ushort)STypeInt.HOUSE_STATUS_INT))
                                            weenies[worldIDQueue[currentWorld][parsed.lord]].WeeniePropertiesInt.Add(new ACE.Database.Models.World.WeeniePropertiesInt { Type = (ushort)STypeInt.HOUSE_STATUS_INT, Value = (int)parsed.prof._bitmask });
                                        if (!weenies[worldIDQueue[currentWorld][parsed.lord]].WeeniePropertiesInt.Any(y => y.Type == (ushort)STypeInt.MIN_LEVEL_INT))
                                            weenies[worldIDQueue[currentWorld][parsed.lord]].WeeniePropertiesInt.Add(new ACE.Database.Models.World.WeeniePropertiesInt { Type = (ushort)STypeInt.MIN_LEVEL_INT, Value = (int)parsed.prof._min_level });
                                        if (!weenies[worldIDQueue[currentWorld][parsed.lord]].WeeniePropertiesInt.Any(y => y.Type == (ushort)STypeInt.MAX_LEVEL_INT))
                                            weenies[worldIDQueue[currentWorld][parsed.lord]].WeeniePropertiesInt.Add(new ACE.Database.Models.World.WeeniePropertiesInt { Type = (ushort)STypeInt.MAX_LEVEL_INT, Value = (int)parsed.prof._max_level });
                                        if (!weenies[worldIDQueue[currentWorld][parsed.lord]].WeeniePropertiesInt.Any(y => y.Type == (ushort)STypeInt.ALLEGIANCE_MIN_LEVEL_INT))
                                            weenies[worldIDQueue[currentWorld][parsed.lord]].WeeniePropertiesInt.Add(new ACE.Database.Models.World.WeeniePropertiesInt { Type = (ushort)STypeInt.ALLEGIANCE_MIN_LEVEL_INT, Value = (int)parsed.prof._min_alleg_rank });
                                        if (!weenies[worldIDQueue[currentWorld][parsed.lord]].WeeniePropertiesInt.Any(y => y.Type == (ushort)STypeInt.ALLEGIANCE_MAX_LEVEL_INT))
                                            weenies[worldIDQueue[currentWorld][parsed.lord]].WeeniePropertiesInt.Add(new ACE.Database.Models.World.WeeniePropertiesInt { Type = (ushort)STypeInt.ALLEGIANCE_MAX_LEVEL_INT, Value = (int)parsed.prof._max_alleg_rank });

                                        if (parsed.prof._type == HouseType.Mansion_HouseType)
                                        {
                                            if (!weenies[worldIDQueue[currentWorld][parsed.lord]].WeeniePropertiesBool.Any(y => y.Type == (ushort)STypeBool.HOUSE_REQUIRES_MONARCH_BOOL))
                                                weenies[worldIDQueue[currentWorld][parsed.lord]].WeeniePropertiesBool.Add(new ACE.Database.Models.World.WeeniePropertiesBool { Type = (ushort)STypeBool.HOUSE_REQUIRES_MONARCH_BOOL, Value = true });
                                        }

                                        if (parsed.prof._buy.list.Count > 0)
                                        {
                                            foreach (var item in parsed.prof._buy.list)
                                            {
                                                if (!weenies[worldIDQueue[currentWorld][parsed.lord]].WeeniePropertiesCreateList.Any(y => y.DestinationType == (sbyte)ACE.Entity.Enum.DestinationType.HouseBuy && y.WeenieClassId == item.wcid))
                                                    weenies[worldIDQueue[currentWorld][parsed.lord]].WeeniePropertiesCreateList.Add(new ACE.Database.Models.World.WeeniePropertiesCreateList { DestinationType = (sbyte)ACE.Entity.Enum.DestinationType.HouseBuy, WeenieClassId = item.wcid, StackSize = item.num, TryToBond = false });
                                            }
                                        }

                                        if (parsed.prof._rent.list.Count > 0)
                                        {
                                            foreach (var item in parsed.prof._rent.list)
                                            {
                                                if (!weenies[worldIDQueue[currentWorld][parsed.lord]].WeeniePropertiesCreateList.Any(y => y.DestinationType == (sbyte)ACE.Entity.Enum.DestinationType.HouseRent && y.WeenieClassId == item.wcid))
                                                    weenies[worldIDQueue[currentWorld][parsed.lord]].WeeniePropertiesCreateList.Add(new ACE.Database.Models.World.WeeniePropertiesCreateList { DestinationType = (sbyte)ACE.Entity.Enum.DestinationType.HouseRent, WeenieClassId = item.wcid, StackSize = item.num, TryToBond = false });
                                            }
                                        }

                                        totalHits++;
                                    }
                                }
                            }
                        }
                        catch (EndOfStreamException) // This can happen when a frag is incomplete and we try to parse it
                        {
                            totalExceptions++;
                        }
                    }
                }

                RegenerateMissingWeenies(weenies, vendorPWDs, itemTemplates, weenieNames);

                CleanupWeenies(weenies);

                foreach (var weenie in weenies.Values)
                    weenie.LastModified = exportTime;

                WeenieSQLWriter.WriteFiles(weenies.Values, txtOutputFolder.Text + "\\9 WeenieDefaults\\SQL\\", weenieNames, null, null, weenies, true);

                var landblocks = new List<ACE.Database.Models.World.LandblockInstance>();

                //if (!weenieNames.ContainsKey(1154))
                //    weenieNames.Add(1154, "Linkable Monster Generator");
                //if (!weenieNames.ContainsKey(1542))
                //    weenieNames.Add(1542, "Linkable Item Generator");
                //if (!weenieNames.ContainsKey(5085))
                //    weenieNames.Add(5085, "Linkable Item Gen - 25 seconds");
                //if (!weenieNames.ContainsKey(28282))
                //    weenieNames.Add(28282, "Linkable Monster Gen - 10 sec.");
                //if (!weenieNames.ContainsKey(15759))
                //    weenieNames.Add(15759, "Linkable Item Generator");

                //foreach (var landblock in instances.Values)
                //{
                //    var lastGuid = landblock.StaticObjects.Keys.OrderBy(x => x).LastOrDefault();

                //    if (lastGuid == 0)
                //    {
                //        var lbid = instances.Where(y => y.Value == landblock).FirstOrDefault();

                //        lastGuid = 0x70000000 | lbid.Key << 12;
                //    }

                //    if (landblock.LinkableMonsterObjects.Values.Count > 0)
                //    {
                //        var first = landblock.LinkableMonsterObjects.Values.FirstOrDefault();
                //        var generatorGuid = ++lastGuid;
                //        var generator = new ACE.Database.Models.World.LandblockInstance
                //        {
                //            Guid = generatorGuid,
                //            //WeenieClassId = 28282,
                //            WeenieClassId = 1154,
                //            ObjCellId = first.ObjCellId,
                //            OriginX = first.OriginX,
                //            OriginY = first.OriginY,
                //            OriginZ = first.OriginZ,
                //            AnglesW = first.AnglesW,
                //            AnglesX = first.AnglesX,
                //            AnglesY = first.AnglesY,
                //            AnglesZ = first.AnglesZ,
                //            IsLinkChild = false
                //        };
                //        landblock.StaticObjects.Add(generatorGuid, generator);
                //        foreach (var item in landblock.LinkableMonsterObjects.Values)
                //        {
                //            item.IsLinkChild = true;
                //            item.Guid = ++lastGuid;
                //            generator.LandblockInstanceLink.Add(new LandblockInstanceLink { ParentGuid = generatorGuid, ChildGuid = item.Guid });
                //        }
                //    }

                //    if (landblock.LinkableNPCObjects.Values.Count > 0)
                //    {
                //        var first = landblock.LinkableNPCObjects.Values.FirstOrDefault();
                //        var generatorGuid = ++lastGuid;
                //        var generator = new ACE.Database.Models.World.LandblockInstance
                //        {
                //            Guid = generatorGuid,
                //            WeenieClassId = 1154,
                //            ObjCellId = first.ObjCellId,
                //            OriginX = first.OriginX,
                //            OriginY = first.OriginY,
                //            OriginZ = first.OriginZ,
                //            AnglesW = first.AnglesW,
                //            AnglesX = first.AnglesX,
                //            AnglesY = first.AnglesY,
                //            AnglesZ = first.AnglesZ,
                //            IsLinkChild = false
                //        };
                //        landblock.StaticObjects.Add(generatorGuid, generator);
                //        foreach (var item in landblock.LinkableNPCObjects.Values)
                //        {
                //            item.IsLinkChild = true;
                //            item.Guid = ++lastGuid;
                //            generator.LandblockInstanceLink.Add(new LandblockInstanceLink { ParentGuid = generatorGuid, ChildGuid = item.Guid });
                //        }
                //    }

                //    if (landblock.LinkableItemObjects.Values.Count > 0)
                //    {
                //        var first = landblock.LinkableItemObjects.Values.FirstOrDefault();
                //        var generatorGuid = ++lastGuid;
                //        var generator = new ACE.Database.Models.World.LandblockInstance
                //        {
                //            Guid = generatorGuid,
                //            //WeenieClassId = 15759,
                //            WeenieClassId = 1542,
                //            ObjCellId = first.ObjCellId,
                //            OriginX = first.OriginX,
                //            OriginY = first.OriginY,
                //            OriginZ = first.OriginZ,
                //            AnglesW = first.AnglesW,
                //            AnglesX = first.AnglesX,
                //            AnglesY = first.AnglesY,
                //            AnglesZ = first.AnglesZ,
                //            IsLinkChild = false
                //        };
                //        landblock.StaticObjects.Add(generatorGuid, generator);
                //        foreach (var item in landblock.LinkableItemObjects.Values)
                //        {
                //            item.IsLinkChild = true;
                //            item.Guid = ++lastGuid;
                //            generator.LandblockInstanceLink.Add(new LandblockInstanceLink { ParentGuid = generatorGuid, ChildGuid = item.Guid });
                //        }
                //    }

                //    ////landblocks.AddRange(landblock.StaticObjects.Values.ToList());

                //    ////landblocks.AddRange(landblock.LinkableMonsterObjects.Values.ToList());

                //    ////landblocks.AddRange(landblock.LinkableNPCObjects.Values.ToList());

                //    ////landblocks.AddRange(landblock.LinkableItemObjects.Values.ToList());
                //}

                //landblocks.Where(x => (x.ObjCellId >> 16) == 0x8603 && x.WeenieClassId == 15759).FirstOrDefault().OriginZ = .005f;
                //landblocks.Where(x => (x.ObjCellId >> 16) == 0x7F03 && x.WeenieClassId == 15759).FirstOrDefault().OriginZ = .005f;
                //landblocks.Where(x => (x.ObjCellId >> 16) == 0x8C04 && x.WeenieClassId == 15759).FirstOrDefault().OriginZ = .005f;
                //landblocks.Where(x => (x.ObjCellId >> 16) == 0x7203 && x.WeenieClassId == 15759).FirstOrDefault().OriginZ = .005f;

                //AddObjectsToLandblock(landblocks, weenieNames, 0x8603);
                //AddObjectsToLandblock(landblocks, weenieNames, 0x7F03);
                //AddObjectsToLandblock(landblocks, weenieNames, 0x8C04);
                //AddObjectsToLandblock(landblocks, weenieNames, 0x7203);

                //CloneLandblockToAnother(landblocks, 0x8603, 0x8602);
                //CloneLandblockToAnother(landblocks, 0x8603, 0x8604);
                //CloneLandblockToAnother(landblocks, 0x8603, 0x8702);
                //CloneLandblockToAnother(landblocks, 0x8603, 0x8703);

                //CloneLandblockToAnother(landblocks, 0x7F03, 0x7F04);
                //CloneLandblockToAnother(landblocks, 0x7F03, 0x8002);
                //CloneLandblockToAnother(landblocks, 0x7F03, 0x8003);
                //CloneLandblockToAnother(landblocks, 0x7F03, 0x8004);

                //CloneLandblockToAnother(landblocks, 0x8C04, 0x8D02);
                //CloneLandblockToAnother(landblocks, 0x8C04, 0x8D03);
                //CloneLandblockToAnother(landblocks, 0x8C04, 0x8D04);
                //CloneLandblockToAnother(landblocks, 0x8C04, 0x8E02);

                //CloneLandblockToAnother(landblocks, 0x7203, 0x7202);
                //CloneLandblockToAnother(landblocks, 0x7203, 0x7204);
                //CloneLandblockToAnother(landblocks, 0x7203, 0x7302);
                //CloneLandblockToAnother(landblocks, 0x7203, 0x7303);

                foreach (var landblock in instances.Values)
                {
                    foreach (var instance in landblock.StaticObjects.Values)
                    {
                        instance.LastModified = exportTime;

                        foreach (var link in instance.LandblockInstanceLink)
                            link.LastModified = exportTime;

                        landblocks.Add(instance);
                    }
                    //foreach (var instance in landblock.LinkableItemObjects.Values)
                    //{
                    //    instance.LastModified = exportTime;

                    //    foreach (var link in instance.LandblockInstanceLink)
                    //        link.LastModified = exportTime;

                    //    landblocks.Add(instance);
                    //}
                    //foreach (var instance in landblock.LinkableMonsterObjects.Values)
                    //{
                    //    instance.LastModified = exportTime;

                    //    foreach (var link in instance.LandblockInstanceLink)
                    //        link.LastModified = exportTime;

                    //    landblocks.Add(instance);
                    //}
                    //foreach (var instance in landblock.LinkableNPCObjects.Values)
                    //{
                    //    instance.LastModified = exportTime;

                    //    foreach (var link in instance.LandblockInstanceLink)
                    //        link.LastModified = exportTime;

                    //    landblocks.Add(instance);
                    //}
                }

                LandblockSQLWriter.WriteFiles(landblocks, txtOutputFolder.Text + "\\6 LandBlockExtendedData\\SQL\\", weenieNames, true);

                MessageBox.Show($"Export started at {start.ToString()}, completed at {DateTime.Now.ToString()} and took {(DateTime.Now - start).TotalMinutes} minutes.");
            }
            finally
            {
                fragDatListFile.CloseFile();

                Interlocked.Increment(ref filesProcessed);
            }
        }

        private void AddObjectsToLandblock(List<LandblockInstance> landblocks, Dictionary<uint, string> weenieNames, uint landblockToAddTo)
        {
            if (!weenieNames.ContainsKey(10762))
                weenieNames.Add(10762, "Portal Linkspot");

            var lastGuid = landblocks.Where(x => (x.ObjCellId >> 16) == landblockToAddTo).OrderBy(x => x.Guid).LastOrDefault().Guid;

            var newGuid = ++lastGuid;

            var newObjCellId = (0x8603021E & 0x0000FFFF) | (landblockToAddTo << 16);

            landblocks.Add(new LandblockInstance {
                Guid = newGuid,
                WeenieClassId = 10762,
                //ObjCellId = newObjCellId, OriginX = 50, OriginY = -54, OriginZ = 0.004999995f, AnglesW = 0.01f, AnglesX = 0, AnglesY = 0, AnglesZ = 0.9f,
                ObjCellId = newObjCellId, OriginX = 50, OriginY = -54, OriginZ = 1, AnglesW = 0.01f, AnglesX = 0, AnglesY = 0, AnglesZ = -1,
                IsLinkChild = true
            });

            var centralCourtyardPortal = landblocks.Where(x => (x.ObjCellId >> 16) == landblockToAddTo && x.WeenieClassId == 31061).FirstOrDefault();

            centralCourtyardPortal.LandblockInstanceLink.Add(new LandblockInstanceLink { ParentGuid = centralCourtyardPortal.Guid, ChildGuid = newGuid });

            newGuid = ++lastGuid;

            newObjCellId = (0x860302C3 & 0x0000FFFF) | (landblockToAddTo << 16);

            landblocks.Add(new LandblockInstance
            {
                Guid = newGuid,
                WeenieClassId = 10762,
                ObjCellId = newObjCellId,
                OriginX = 119,
                OriginY = -141,
                OriginZ = 0.004999995f,
                AnglesW = 1,
                AnglesX = 0,
                AnglesY = 0,
                AnglesZ = 0,
                IsLinkChild = true
            });

            var outerCourtyardPortal = landblocks.Where(x => (x.ObjCellId >> 16) == landblockToAddTo && x.WeenieClassId == 29334).FirstOrDefault();

            outerCourtyardPortal.LandblockInstanceLink.Add(new LandblockInstanceLink { ParentGuid = outerCourtyardPortal.Guid, ChildGuid = newGuid });
        }

        private void CloneLandblockToAnother(List<LandblockInstance> landblocks, uint landblockToCloneFrom, uint landblockToCloneTo)
        {
            //var newLandblock = new List<LandblockInstance>(landblocks.Where(x=>(x.ObjCellId >> 16) == landblockToCloneFrom).OrderBy(y=>y.Guid).ToList());

            var newLandblock = new List<LandblockInstance>();

            foreach (var instance in landblocks.Where(x => (x.ObjCellId >> 16) == landblockToCloneFrom).OrderBy(y => y.Guid))
            {
                var newGuid = (instance.Guid & 0xF0000FFF) | (landblockToCloneTo << 12);
                var newObjCellId = (instance.ObjCellId & 0x0000FFFF) | (landblockToCloneTo << 16);

                var newInstance = new LandblockInstance
                {
                    AnglesW = instance.AnglesW,
                    AnglesX = instance.AnglesX,
                    AnglesY = instance.AnglesY,
                    AnglesZ = instance.AnglesZ,
                    Guid = newGuid,
                    IsLinkChild = instance.IsLinkChild,
                    ObjCellId = newObjCellId,
                    OriginX = instance.OriginX,
                    OriginY = instance.OriginY,
                    OriginZ = instance.OriginZ,
                    WeenieClassId = instance.WeenieClassId
                };

                foreach(var link in instance.LandblockInstanceLink)
                {
                    var newChildGuid = (link.ChildGuid & 0xF0000FFF) | (landblockToCloneTo << 12);
                    newInstance.LandblockInstanceLink.Add(new LandblockInstanceLink { ChildGuid = newChildGuid, ParentGuid = newGuid });
                }

                newLandblock.Add(newInstance);
            }

            var landblocksToRemove = landblocks.Where(x => (x.ObjCellId >> 16) == landblockToCloneTo).OrderBy(y => y.Guid).ToList();

            foreach (var landblock in landblocksToRemove)
            {
                landblocks.Remove(landblock);
            }

            landblocks.AddRange(newLandblock);
        }

        private void RegenerateMissingWeenies(Dictionary<uint, Weenie> weenies, Dictionary<uint, CM_Physics.PublicWeenieDesc> vendorPWDs, Dictionary<int, CM_Physics.CreateObject> itemTemplates, Dictionary<uint, string> weenieNames)
        {
            var vendors = weenies.Where(w => w.Value.Type == (int)ACE.Entity.Enum.WeenieType.Vendor).ToDictionary(w => w.Key, w => w.Value);

            foreach (var weenie in vendors.Values)
            {
                //if (weenie.Type == (int)ACE.Entity.Enum.WeenieType.Vendor)
                //{
                    var shopList = weenie.WeeniePropertiesCreateList.Where(w => w.DestinationType == (int)ACE.Entity.Enum.DestinationType.Shop).ToList();
                    foreach (var shopItem in shopList)
                    {
                        if (!weenies.ContainsKey(shopItem.WeenieClassId))
                        {
                            var parsed = new CM_Physics.CreateObject();

                            parsed.object_id = vendorPWDs[shopItem.WeenieClassId]._wcid;
                            parsed.wdesc = vendorPWDs[shopItem.WeenieClassId];
                            parsed.objdesc = new CM_Physics.ObjDesc();
                            parsed.physicsdesc = new CM_Physics.PhysicsDesc();

                            var wo = CreateWeenieFromCreateObjectMsg(parsed);

                            SetWeenieType(wo);

                            if (!itemTemplates.ContainsKey(wo.Type))
                            {
                                Console.WriteLine($"couldn't regen {parsed.wdesc._name} ({parsed.wdesc._wcid}) - no item template available for {((WeenieType)wo.Type).ToString()}");
                                continue;
                            }

                            parsed.objdesc = itemTemplates[wo.Type].objdesc;
                            parsed.physicsdesc = itemTemplates[wo.Type].physicsdesc;

                            wo = CreateWeenieFromCreateObjectMsg(parsed);

                            SetWeenieType(wo);

                            wo.WeeniePropertiesDID.Add(new ACE.Database.Models.World.WeeniePropertiesDID { Type = 8044, Value = itemTemplates[wo.Type].wdesc._wcid });

                            //if (!itemTemplates.ContainsKey(wo.Type))
                            //    itemTemplates.Add(wo.Type, parsed);

                            weenies.Add(parsed.wdesc._wcid, wo);

                            if (!weenieNames.ContainsKey(parsed.wdesc._wcid))
                                weenieNames.Add(parsed.wdesc._wcid, parsed.wdesc._name.m_buffer);

                            //if (!processedWeeniePositions.ContainsKey(parsed.wdesc._wcid))
                            //    processedWeeniePositions.Add(parsed.wdesc._wcid, new List<Position>());

                            totalHits++;
                        }
                    }
                //}


                //weenie.LastModified = DateTime.UtcNow;
            }
        }

        private void CleanupWeenies(Dictionary<uint, Weenie> weenies)
        {
            foreach(var weenie in weenies.Values)
            {
                var maxStructure = weenie.WeeniePropertiesInt.FirstOrDefault(y => y.Type == (ushort)ACE.Entity.Enum.Properties.PropertyInt.MaxStructure);
                if (maxStructure != null)
                {
                    var structure = weenie.WeeniePropertiesInt.FirstOrDefault(y => y.Type == (ushort)ACE.Entity.Enum.Properties.PropertyInt.Structure);
                    structure.Value = maxStructure.Value;
                }

                var maxStackSize = weenie.WeeniePropertiesInt.FirstOrDefault(y => y.Type == (ushort)ACE.Entity.Enum.Properties.PropertyInt.MaxStackSize);
                if (maxStackSize != null)
                {
                    var stackSize = weenie.WeeniePropertiesInt.FirstOrDefault(y => y.Type == (ushort)ACE.Entity.Enum.Properties.PropertyInt.StackSize);
                    var burden = weenie.WeeniePropertiesInt.FirstOrDefault(y => y.Type == (ushort)ACE.Entity.Enum.Properties.PropertyInt.EncumbranceVal);
                    var value = weenie.WeeniePropertiesInt.FirstOrDefault(y => y.Type == (ushort)ACE.Entity.Enum.Properties.PropertyInt.Value);

                    int calcBurden = 0;
                    int calcValue = 0;

                    if (burden != null && stackSize != null)
                        calcBurden = burden.Value / stackSize.Value;
                    if (value != null && stackSize != null)
                        calcValue = value.Value / stackSize.Value;

                    if (!weenie.WeeniePropertiesInt.Any(y => y.Type == (ushort)ACE.Entity.Enum.Properties.PropertyInt.StackUnitEncumbrance))
                        weenie.WeeniePropertiesInt.Add(new ACE.Database.Models.World.WeeniePropertiesInt { Type = (ushort)ACE.Entity.Enum.Properties.PropertyInt.StackUnitEncumbrance, Value = calcBurden });

                    if (!weenie.WeeniePropertiesInt.Any(y => y.Type == (ushort)ACE.Entity.Enum.Properties.PropertyInt.StackUnitValue))
                        weenie.WeeniePropertiesInt.Add(new ACE.Database.Models.World.WeeniePropertiesInt { Type = (ushort)ACE.Entity.Enum.Properties.PropertyInt.StackUnitValue, Value = calcValue });

                    if (burden != null)
                        burden.Value = calcBurden;
                    if (value != null)
                        value.Value = calcValue;
                    if (stackSize != null)
                        stackSize.Value = 1;
                }

                var container = weenie.WeeniePropertiesIID.FirstOrDefault(y => y.Type == (ushort)ACE.Entity.Enum.Properties.PropertyInstanceId.Container);
                if (container != null)
                    weenie.WeeniePropertiesIID.Remove(container);

                var wielder = weenie.WeeniePropertiesIID.FirstOrDefault(y => y.Type == (ushort)ACE.Entity.Enum.Properties.PropertyInstanceId.Wielder);
                if (wielder != null)
                    weenie.WeeniePropertiesIID.Remove(wielder);

                var houseOwner = weenie.WeeniePropertiesIID.FirstOrDefault(y => y.Type == (ushort)ACE.Entity.Enum.Properties.PropertyInstanceId.HouseOwner);
                if (houseOwner != null)
                    weenie.WeeniePropertiesIID.Remove(houseOwner);

                var petOwner = weenie.WeeniePropertiesIID.FirstOrDefault(y => y.Type == (ushort)ACE.Entity.Enum.Properties.PropertyInstanceId.PetOwner);
                if (petOwner != null)
                    weenie.WeeniePropertiesIID.Remove(petOwner);

                var patron = weenie.WeeniePropertiesIID.FirstOrDefault(y => y.Type == (ushort)ACE.Entity.Enum.Properties.PropertyInstanceId.Patron);
                if (patron != null)
                    weenie.WeeniePropertiesIID.Remove(patron);

                var monarch = weenie.WeeniePropertiesIID.FirstOrDefault(y => y.Type == (ushort)ACE.Entity.Enum.Properties.PropertyInstanceId.Monarch);
                if (monarch != null)
                    weenie.WeeniePropertiesIID.Remove(monarch);

                var allegiance = weenie.WeeniePropertiesIID.FirstOrDefault(y => y.Type == (ushort)ACE.Entity.Enum.Properties.PropertyInstanceId.Allegiance);
                if (allegiance != null)
                    weenie.WeeniePropertiesIID.Remove(allegiance);

                if (weenie.Type == (int)ACE.Entity.Enum.WeenieType.House)
                {
                    var name = weenie.WeeniePropertiesString.FirstOrDefault(y => y.Type == (ushort)ACE.Entity.Enum.Properties.PropertyString.Name);

                    if (name.Value == "Apartment")
                    {
                        if (!weenie.WeeniePropertiesInt.Any(y => y.Type == (ushort)ACE.Entity.Enum.Properties.PropertyInt.HouseType))
                            weenie.WeeniePropertiesInt.Add(new ACE.Database.Models.World.WeeniePropertiesInt { Type = (ushort)ACE.Entity.Enum.Properties.PropertyInt.HouseType, Value = (int)HouseType.Apartment_HouseType });
                    }
                    else if(name.Value == "Cottage")
                    {
                        if (!weenie.WeeniePropertiesInt.Any(y => y.Type == (ushort)ACE.Entity.Enum.Properties.PropertyInt.HouseType))
                            weenie.WeeniePropertiesInt.Add(new ACE.Database.Models.World.WeeniePropertiesInt { Type = (ushort)ACE.Entity.Enum.Properties.PropertyInt.HouseType, Value = (int)HouseType.Cottage_HouseType });
                    }
                    else if (name.Value == "Villa")
                    {
                        if (!weenie.WeeniePropertiesInt.Any(y => y.Type == (ushort)ACE.Entity.Enum.Properties.PropertyInt.HouseType))
                            weenie.WeeniePropertiesInt.Add(new ACE.Database.Models.World.WeeniePropertiesInt { Type = (ushort)ACE.Entity.Enum.Properties.PropertyInt.HouseType, Value = (int)HouseType.Villa_HouseType });
                    }
                    else if (name.Value == "Mansion")
                    {
                        if (!weenie.WeeniePropertiesInt.Any(y => y.Type == (ushort)ACE.Entity.Enum.Properties.PropertyInt.HouseType))
                            weenie.WeeniePropertiesInt.Add(new ACE.Database.Models.World.WeeniePropertiesInt { Type = (ushort)ACE.Entity.Enum.Properties.PropertyInt.HouseType, Value = (int)HouseType.Mansion_HouseType });
                    }
                }

                if (weenie.Type == (int)ACE.Entity.Enum.WeenieType.SlumLord)
                {
                    var name = weenie.WeeniePropertiesString.FirstOrDefault(y => y.Type == (ushort)ACE.Entity.Enum.Properties.PropertyString.Name);

                    if (name.Value.EndsWith(" Apartment"))
                        name.Value = "Apartment";
                    else if (name.Value.EndsWith(" Cottage"))
                        name.Value = "Cottage";
                    else if (name.Value.EndsWith(" Villa"))
                        name.Value = "Villa";
                    else if (name.Value.EndsWith(" Mansion"))
                        name.Value = "Mansion";
                }

                var list = weenie.WeeniePropertiesString.ToList();
                foreach (var str in list)
                {
                    if (str.Value == null)
                        weenie.WeeniePropertiesString.Remove(str);
                }

                if (weenie.Type == (int)ACE.Entity.Enum.WeenieType.Pet || weenie.Type == (int)ACE.Entity.Enum.WeenieType.CombatPet)
                {
                    var name = weenie.WeeniePropertiesString.FirstOrDefault(y => y.Type == (ushort)ACE.Entity.Enum.Properties.PropertyString.Name);

                    int indexOfess = name.Value.LastIndexOf("'s ");

                    var newName = name.Value.Substring(indexOfess + 3);

                    if (indexOfess > 0)
                        name.Value = newName;
                }

                foreach (var flo in weenie.WeeniePropertiesFloat)
                {
                    flo.Value = Math.Round(flo.Value, 3);
                }

                foreach (var intV in weenie.WeeniePropertiesInt)
                {
                    if (intV.Type == (ushort)ACE.Entity.Enum.Properties.PropertyInt.AppraisalPages)
                        intV.Type = 8042;
                    if (intV.Type == (ushort)ACE.Entity.Enum.Properties.PropertyInt.AppraisalMaxPages)
                        intV.Type = 8043;

                    if (intV.Type == (ushort)ACE.Entity.Enum.Properties.PropertyInt.ItemsCapacity && intV.Value == 255)
                        intV.Value = -1;
                    if (intV.Type == (ushort)ACE.Entity.Enum.Properties.PropertyInt.ContainersCapacity && intV.Value == 255)
                        intV.Value = -1;
                }

                var appraisalItemSkill = weenie.WeeniePropertiesInt.FirstOrDefault(y => y.Type == (ushort)ACE.Entity.Enum.Properties.PropertyInt.AppraisalItemSkill);
                if (appraisalItemSkill != null)
                {
                    weenie.WeeniePropertiesInt.Remove(appraisalItemSkill);

                    weenie.WeeniePropertiesDID.Add(new WeeniePropertiesDID { Type = (ushort)ACE.Entity.Enum.Properties.PropertyDataId.ItemSkillLimit, Value = (uint)appraisalItemSkill.Value });
                }

                var lockpickSuccess = weenie.WeeniePropertiesInt.FirstOrDefault(y => y.Type == (ushort)ACE.Entity.Enum.Properties.PropertyInt.AppraisalLockpickSuccessPercent);
                if (lockpickSuccess != null)
                    weenie.WeeniePropertiesInt.Remove(lockpickSuccess);

                var appraisalLongDescDecoration = weenie.WeeniePropertiesInt.FirstOrDefault(y => y.Type == (ushort)ACE.Entity.Enum.Properties.PropertyInt.AppraisalLongDescDecoration);
                if (appraisalLongDescDecoration != null)
                    weenie.WeeniePropertiesInt.Remove(appraisalLongDescDecoration);

                var openLock = weenie.WeeniePropertiesBool.Where(y => y.Type == (ushort)ACE.Entity.Enum.Properties.PropertyBool.Open || y.Type == (ushort)ACE.Entity.Enum.Properties.PropertyBool.Locked).ToList();
                foreach (var prop in openLock)
                {
                    if (prop.Type == (ushort)ACE.Entity.Enum.Properties.PropertyBool.Open)
                    {
                        if (prop.Value)
                        {
                            weenie.WeeniePropertiesBool.Remove(prop);
                            prop.Value = false;
                            weenie.WeeniePropertiesBool.Add(prop);
                        }
                        weenie.WeeniePropertiesBool.Add(new WeeniePropertiesBool { Type = (ushort)ACE.Entity.Enum.Properties.PropertyBool.DefaultOpen, Value = false });
                    }

                    if (prop.Type == (ushort)ACE.Entity.Enum.Properties.PropertyBool.Locked)
                    {
                        if (prop.Value)
                        {
                            weenie.WeeniePropertiesBool.Add(new WeeniePropertiesBool { Type = (ushort)ACE.Entity.Enum.Properties.PropertyBool.DefaultLocked, Value = true });
                        }
                        //else
                        //{
                        //    weenie.WeeniePropertiesBool.Remove(prop);
                        //}

                    }
                }

                var physicsState = weenie.WeeniePropertiesInt.FirstOrDefault(y => y.Type == (ushort)ACE.Entity.Enum.Properties.PropertyInt.PhysicsState);
                if (physicsState != null)
                {
                    var ps = (ACE.Entity.Enum.PhysicsState)physicsState.Value;
                    ps &= ~ACE.Entity.Enum.PhysicsState.HasPhysicsBSP;
                    physicsState.Value = (int)ps;
                }

                if (weenie.Type == (int)ACE.Entity.Enum.WeenieType.Portal)
                {
                    var portalBitmask = weenie.WeeniePropertiesInt.FirstOrDefault(y => y.Type == (ushort)ACE.Entity.Enum.Properties.PropertyInt.PortalBitmask);
                    var portalDest = weenie.WeeniePropertiesString.FirstOrDefault(y => y.Type == (ushort)ACE.Entity.Enum.Properties.PropertyString.AppraisalPortalDestination);

                    if (portalBitmask != null && portalDest == null)
                        weenie.WeeniePropertiesBool.Add(new WeeniePropertiesBool { Type = (ushort)ACE.Entity.Enum.Properties.PropertyBool.PortalShowDestination, Value = false });

                    if (portalDest != null)
                        weenie.WeeniePropertiesString.Remove(portalDest);
                }

                var procSpell = weenie.WeeniePropertiesDID.FirstOrDefault(y => y.Type == (ushort)ACE.Entity.Enum.Properties.PropertyDataId.ProcSpell);
                if (procSpell != null)
                {
                    var spell = weenie.GetSpell((int)procSpell.Value);
                    if (spell != null)
                        weenie.WeeniePropertiesSpellBook.Remove(spell);
                }

                var didSpell = weenie.WeeniePropertiesDID.FirstOrDefault(y => y.Type == (ushort)ACE.Entity.Enum.Properties.PropertyDataId.Spell);
                if (didSpell != null)
                {
                    var spell = weenie.GetSpell((int)didSpell.Value);
                    if (spell != null)
                        weenie.WeeniePropertiesSpellBook.Remove(spell);
                }

                //var pcapBools = weenie.WeeniePropertiesBool.ToList();
                //foreach (var prop in pcapBools)
                //{
                //    if (prop.Type >= 8000)
                //        weenie.WeeniePropertiesBool.Remove(prop);
                //}
                //var pcapDids = weenie.WeeniePropertiesDID.ToList();
                //foreach (var prop in pcapDids)
                //{
                //    if (prop.Type == 8044) continue;

                //    if (prop.Type >= 8000)
                //        weenie.WeeniePropertiesDID.Remove(prop);
                //}
                //var pcapFloats = weenie.WeeniePropertiesFloat.ToList();
                //foreach (var prop in pcapFloats)
                //{
                //    if (prop.Type >= 8000)
                //        weenie.WeeniePropertiesFloat.Remove(prop);
                //}
                //var pcapIids = weenie.WeeniePropertiesIID.ToList();
                //foreach (var prop in pcapIids)
                //{
                //    if (prop.Type >= 8000)
                //        weenie.WeeniePropertiesIID.Remove(prop);
                //}
                //var pcapInts = weenie.WeeniePropertiesInt.ToList();
                //foreach (var prop in pcapInts)
                //{
                //    if (prop.Type >= 8000)
                //        weenie.WeeniePropertiesInt.Remove(prop);
                //}
                //var pcapInt64s = weenie.WeeniePropertiesInt64.ToList();
                //foreach (var prop in pcapInt64s)
                //{
                //    if (prop.Type >= 8000)
                //        weenie.WeeniePropertiesInt64.Remove(prop);
                //}
                ////var pcapPoss = weenie.WeeniePropertiesPosition.ToList();
                ////foreach (var prop in pcapPoss)
                ////{
                ////    if (prop.PositionType >= 8000)
                ////        weenie.WeeniePropertiesPosition.Remove(prop);
                ////}
                //var pcapStrs = weenie.WeeniePropertiesString.ToList();
                //foreach (var prop in pcapStrs)
                //{
                //    if (prop.Type >= 8000)
                //        weenie.WeeniePropertiesString.Remove(prop);
                //}
                //weenie.WeeniePropertiesAnimPart.Clear();
                //weenie.WeeniePropertiesPalette.Clear();
                //weenie.WeeniePropertiesTextureMap.Clear();

                weenie.LastModified = DateTime.UtcNow;
            }
        }

        private void SetWeenieType(ACE.Database.Models.World.Weenie wo)
        {
            var objectDescriptionFlag = (ACE.Entity.Enum.ObjectDescriptionFlag)wo.GetProperty((ACE.Entity.Enum.Properties.PropertyDataId)8003);

            if (objectDescriptionFlag.HasFlag(ACE.Entity.Enum.ObjectDescriptionFlag.LifeStone))
            {
                wo.Type = (int)ACE.Entity.Enum.WeenieType.LifeStone;
                return;
            }
            else if (objectDescriptionFlag.HasFlag(ACE.Entity.Enum.ObjectDescriptionFlag.BindStone))
            {
                wo.Type = (int)ACE.Entity.Enum.WeenieType.AllegianceBindstone;
                return;
            }
            else if (objectDescriptionFlag.HasFlag(ACE.Entity.Enum.ObjectDescriptionFlag.PkSwitch))
            {
                wo.Type = (int)ACE.Entity.Enum.WeenieType.PKModifier;
                return;
            }
            else if (objectDescriptionFlag.HasFlag(ACE.Entity.Enum.ObjectDescriptionFlag.NpkSwitch))
            {
                wo.Type = (int)ACE.Entity.Enum.WeenieType.PKModifier;
                return;
            }
            else if (objectDescriptionFlag.HasFlag(ACE.Entity.Enum.ObjectDescriptionFlag.Lockpick))
            {
                wo.Type = (int)ACE.Entity.Enum.WeenieType.Lockpick;
                return;
            }
            else if (objectDescriptionFlag.HasFlag(ACE.Entity.Enum.ObjectDescriptionFlag.Food))
            {
                wo.Type = (int)ACE.Entity.Enum.WeenieType.Food;
                return;
            }
            else if (objectDescriptionFlag.HasFlag(ACE.Entity.Enum.ObjectDescriptionFlag.Healer))
            {
                wo.Type = (int)ACE.Entity.Enum.WeenieType.Healer;
                return;
            }
            else if (objectDescriptionFlag.HasFlag(ACE.Entity.Enum.ObjectDescriptionFlag.Book))
            {
                wo.Type = (int)ACE.Entity.Enum.WeenieType.Book;
                return;
            }
            else if (objectDescriptionFlag.HasFlag(ACE.Entity.Enum.ObjectDescriptionFlag.Portal))
            {
                wo.Type = (int)ACE.Entity.Enum.WeenieType.Portal;
                return;
            }
            else if (objectDescriptionFlag.HasFlag(ACE.Entity.Enum.ObjectDescriptionFlag.Door))
            {
                wo.Type = (int)ACE.Entity.Enum.WeenieType.Door;
                return;
            }
            else if (objectDescriptionFlag.HasFlag(ACE.Entity.Enum.ObjectDescriptionFlag.Vendor))
            {
                wo.Type = (int)ACE.Entity.Enum.WeenieType.Vendor;
                return;
            }
            else if (objectDescriptionFlag.HasFlag(ACE.Entity.Enum.ObjectDescriptionFlag.Admin))
            {
                wo.Type = (int)ACE.Entity.Enum.WeenieType.Admin;
                return;
            }
            else if (objectDescriptionFlag.HasFlag(ACE.Entity.Enum.ObjectDescriptionFlag.Corpse))
            {
                wo.Type = (int)ACE.Entity.Enum.WeenieType.Corpse;
                return;
            }

            if (wo.GetProperty(ACE.Entity.Enum.Properties.PropertyInt.ValidLocations) == (int)ACE.Entity.Enum.EquipMask.MissileAmmo)
            {
                wo.Type = (int)ACE.Entity.Enum.WeenieType.Ammunition;
                return;
            }

            var itemType = (ACE.Entity.Enum.ItemType)wo.GetProperty(ACE.Entity.Enum.Properties.PropertyInt.ItemType);

            switch (itemType)
            {
                case ACE.Entity.Enum.ItemType.Misc:
                    if (
                           wo.ClassId == 9548 || // W_HOUSE_CLASS
                           wo.ClassId >= 9693 && wo.ClassId <= 10492 || // W_HOUSECOTTAGE1_CLASS to W_HOUSECOTTAGE800_CLASS
                           wo.ClassId >= 10493 && wo.ClassId <= 10662 || // W_HOUSEVILLA801_CLASS to W_HOUSEVILLA970_CLASS
                           wo.ClassId >= 10663 && wo.ClassId <= 10692 || // W_HOUSEMANSION971_CLASS to W_HOUSEMANSION1000_CLASS
                           wo.ClassId >= 10746 && wo.ClassId <= 10750 || // W_HOUSETEST1_CLASS to W_HOUSETEST5_CLASS
                           wo.ClassId >= 10829 && wo.ClassId <= 10839 || // W_HOUSETEST6_CLASS to W_HOUSETEST16_CLASS
                           wo.ClassId >= 11677 && wo.ClassId <= 11682 || // W_HOUSETEST17_CLASS to W_HOUSETEST22_CLASS
                           wo.ClassId >= 12311 && wo.ClassId <= 12460 || // W_HOUSECOTTAGE1001_CLASS to W_HOUSECOTTAGE1150_CLASS
                           wo.ClassId >= 12775 && wo.ClassId <= 13024 || // W_HOUSECOTTAGE1151_CLASS to W_HOUSECOTTAGE1400_CLASS
                           wo.ClassId >= 13025 && wo.ClassId <= 13064 || // W_HOUSEVILLA1401_CLASS to W_HOUSEVILLA1440_CLASS
                           wo.ClassId >= 13065 && wo.ClassId <= 13074 || // W_HOUSEMANSION1441_CLASS to W_HOUSEMANSION1450_CLASS
                           wo.ClassId == 13234 || // W_HOUSECOTTAGETEST10000_CLASS
                           wo.ClassId == 13235 || // W_HOUSEVILLATEST10001_CLASS
                           wo.ClassId >= 13243 && wo.ClassId <= 14042 || // W_HOUSECOTTAGE1451_CLASS to W_HOUSECOTTAGE2350_CLASS
                           wo.ClassId >= 14043 && wo.ClassId <= 14222 || // W_HOUSEVILLA1851_CLASS to W_HOUSEVILLA2440_CLASS
                           wo.ClassId >= 14223 && wo.ClassId <= 14242 || // W_HOUSEMANSION1941_CLASS to W_HOUSEMANSION2450_CLASS
                           wo.ClassId >= 14938 && wo.ClassId <= 15087 || // W_HOUSECOTTAGE2451_CLASS to W_HOUSECOTTAGE2600_CLASS
                           wo.ClassId >= 15088 && wo.ClassId <= 15127 || // W_HOUSEVILLA2601_CLASS to W_HOUSEVILLA2640_CLASS
                           wo.ClassId >= 15128 && wo.ClassId <= 15137 || // W_HOUSEMANSION2641_CLASS to W_HOUSEMANSION2650_CLASS
                           wo.ClassId >= 15452 && wo.ClassId <= 15457 || // W_HOUSEAPARTMENT2851_CLASS to W_HOUSEAPARTMENT2856_CLASS
                           wo.ClassId >= 15458 && wo.ClassId <= 15607 || // W_HOUSECOTTAGE2651_CLASS to W_HOUSECOTTAGE2800_CLASS
                           wo.ClassId >= 15612 && wo.ClassId <= 15661 || // W_HOUSEVILLA2801_CLASS to W_HOUSEVILLA2850_CLASS
                           wo.ClassId >= 15897 && wo.ClassId <= 16890 || // W_HOUSEAPARTMENT2857_CLASS to W_HOUSEAPARTMENT3850_CLASS
                           wo.ClassId >= 16923 && wo.ClassId <= 18923 || // W_HOUSEAPARTMENT4051_CLASS to W_HOUSEAPARTMENT6050_CLASS
                           wo.ClassId >= 18924 && wo.ClassId <= 19073 || // W_HOUSECOTTAGE3851_CLASS to W_HOUSECOTTAGE4000_CLASS
                           wo.ClassId >= 19077 && wo.ClassId <= 19126 || // W_HOUSEVILLA4001_CLASS to W_HOUSEVILLA4050_CLASS
                           wo.ClassId >= 20650 && wo.ClassId <= 20799 || // W_HOUSECOTTAGE6051_CLASS to W_HOUSECOTTAGE6200_CLASS
                           wo.ClassId >= 20800 && wo.ClassId <= 20839 || // W_HOUSEVILLA6201_CLASS to W_HOUSEVILLA6240_CLASS
                           wo.ClassId >= 20840 && wo.ClassId <= 20849    // W_HOUSEMANSION6241_CLASS to W_HOUSEMANSION6250_CLASS
                           )
                        wo.Type = (int)ACE.Entity.Enum.WeenieType.House;
                    else if (wo.GetProperty(ACE.Entity.Enum.Properties.PropertyString.Name).Contains("Deed"))
                        wo.Type = (int)ACE.Entity.Enum.WeenieType.Deed;
                    else if (wo.GetProperty(ACE.Entity.Enum.Properties.PropertyString.Name).Contains("Button") ||
                        wo.GetProperty(ACE.Entity.Enum.Properties.PropertyString.Name).Contains("Lever") && !wo.GetProperty(ACE.Entity.Enum.Properties.PropertyString.Name).Contains("Broken")
                        || wo.GetProperty(ACE.Entity.Enum.Properties.PropertyString.Name).Contains("Candle") && !wo.GetProperty(ACE.Entity.Enum.Properties.PropertyString.Name).Contains("Floating") && !wo.GetProperty(ACE.Entity.Enum.Properties.PropertyString.Name).Contains("Bronze")
                        || wo.GetProperty(ACE.Entity.Enum.Properties.PropertyString.Name).Contains("Torch") && wo.ClassId != 293
                        || wo.GetProperty(ACE.Entity.Enum.Properties.PropertyString.Name).Contains("Plant") && !wo.GetProperty(ACE.Entity.Enum.Properties.PropertyString.Name).Contains("Fertilized")
                        )
                        wo.Type = (int)ACE.Entity.Enum.WeenieType.Switch;
                    else if (wo.GetProperty(ACE.Entity.Enum.Properties.PropertyString.Name).Contains("Essence") && wo.GetProperty(ACE.Entity.Enum.Properties.PropertyInt.MaxStructure) == 50)
                        wo.Type = (int)ACE.Entity.Enum.WeenieType.PetDevice;
                    else if (wo.GetProperty(ACE.Entity.Enum.Properties.PropertyString.Name).Contains("Mag-Ma!")
                        || wo.GetProperty(ACE.Entity.Enum.Properties.PropertyString.Name) == "Acid"
                        || wo.GetProperty(ACE.Entity.Enum.Properties.PropertyString.Name).Contains("Vent")
                        || wo.GetProperty(ACE.Entity.Enum.Properties.PropertyString.Name).Contains("Steam")
                        || wo.GetProperty(ACE.Entity.Enum.Properties.PropertyString.Name).Contains("Electric Floor")
                        || wo.GetProperty(ACE.Entity.Enum.Properties.PropertyString.Name).Contains("Refreshing")
                        || wo.GetProperty(ACE.Entity.Enum.Properties.PropertyString.Name) == "Sewer"
                        //|| parsed.wdesc._name.m_buffer.Contains("Ice") && !parsed.wdesc._name.m_buffer.Contains("Box")
                        //|| parsed.wdesc._name.m_buffer.Contains("Firespurt")
                        || wo.GetProperty(ACE.Entity.Enum.Properties.PropertyString.Name).Contains("Flames")
                        || wo.GetProperty(ACE.Entity.Enum.Properties.PropertyString.Name).Contains("Plume")
                        || wo.GetProperty(ACE.Entity.Enum.Properties.PropertyString.Name).Contains("The Black Breath")
                        //|| parsed.wdesc._name.m_buffer.Contains("Bonfire")
                        || wo.GetProperty(ACE.Entity.Enum.Properties.PropertyString.Name).Contains("Geyser")
                        || wo.GetProperty(ACE.Entity.Enum.Properties.PropertyString.Name).Contains("Magma")
                        || wo.ClassId == 14805
                        //|| parsed.wdesc._name.m_buffer.Contains("Pool") && !parsed.wdesc._name.m_buffer.Contains("of")
                        || wo.GetProperty(ACE.Entity.Enum.Properties.PropertyString.Name).Contains("Firespurt")
                        || wo.GetProperty(ACE.Entity.Enum.Properties.PropertyString.Name).Contains("Bonfire")
                        || wo.GetProperty(ACE.Entity.Enum.Properties.PropertyString.Name).Contains("Pool") && !wo.GetProperty(ACE.Entity.Enum.Properties.PropertyString.Name).Contains("of")
                        )
                        wo.Type = (int)ACE.Entity.Enum.WeenieType.HotSpot;
                    else
                        goto default;
                    break;
                case ACE.Entity.Enum.ItemType.Caster:
                    wo.Type = (int)ACE.Entity.Enum.WeenieType.Caster;
                    break;
                case ACE.Entity.Enum.ItemType.Jewelry:
                    wo.Type = (int)ACE.Entity.Enum.WeenieType.Generic;
                    break;
                case ACE.Entity.Enum.ItemType.Armor:
                    if ((wo.GetProperty(ACE.Entity.Enum.Properties.PropertyInt.CombatUse) ?? 0) == (int)ACE.Entity.Enum.CombatUse.Shield)
                        wo.Type = (int)ACE.Entity.Enum.WeenieType.Generic;
                    else
                        wo.Type = (int)ACE.Entity.Enum.WeenieType.Clothing;
                    break;
                case ACE.Entity.Enum.ItemType.Clothing:
                    wo.Type = (int)ACE.Entity.Enum.WeenieType.Clothing;
                    break;
                case ACE.Entity.Enum.ItemType.Container:
                    if (
                        wo.ClassId == 9686 || // W_HOOK_CLASS
                        wo.ClassId == 11697 || // W_HOOK_FLOOR_CLASS
                        wo.ClassId == 11698 || // W_HOOK_CEILING_CLASS
                        wo.ClassId == 12678 || // W_HOOK_ROOF_CLASS
                        wo.ClassId == 12679    // W_HOOK_YARD_CLASS
                        )
                        wo.Type = (int)ACE.Entity.Enum.WeenieType.Hook;
                    else if (
                        wo.ClassId == 9687     // W_STORAGE_CLASS
                        )
                        wo.Type = (int)ACE.Entity.Enum.WeenieType.Storage;
                    else if (
                        wo.GetProperty(ACE.Entity.Enum.Properties.PropertyString.Name).Contains("Pack")
                        || wo.GetProperty(ACE.Entity.Enum.Properties.PropertyString.Name).Contains("Backpack")
                        || wo.GetProperty(ACE.Entity.Enum.Properties.PropertyString.Name).Contains("Sack")
                        || wo.GetProperty(ACE.Entity.Enum.Properties.PropertyString.Name).Contains("Pouch")
                        || wo.GetProperty(ACE.Entity.Enum.Properties.PropertyString.Name).Contains("Basket")
                        )
                        wo.Type = (int)ACE.Entity.Enum.WeenieType.Container;
                    else
                        wo.Type = (int)ACE.Entity.Enum.WeenieType.Chest;
                    break;
                case ACE.Entity.Enum.ItemType.None:
                    if (
                        wo.ClassId == 9621 || // W_SLUMLORD_CLASS
                        wo.ClassId == 10752 || // W_SLUMLORDTESTCHEAP_CLASS
                        wo.ClassId == 10753 || // W_SLUMLORDTESTEXPENSIVE_CLASS
                        wo.ClassId == 10754 || // W_SLUMLORDTESTMODERATE_CLASS
                        wo.ClassId == 11711 || // W_SLUMLORDCOTTAGECHEAP_CLASS
                        wo.ClassId == 11712 || // W_SLUMLORDCOTTAGEEXPENSIVE_CLASS
                        wo.ClassId == 11713 || // W_SLUMLORDCOTTAGEMODERATE_CLASS
                        wo.ClassId == 11714 || // W_SLUMLORDMANSIONCHEAP_CLASS
                        wo.ClassId == 11715 || // W_SLUMLORDMANSIONEXPENSIVE_CLASS
                        wo.ClassId == 11716 || // W_SLUMLORDMANSIONMODERATE_CLASS
                        wo.ClassId == 11717 || // W_SLUMLORDVILLACHEAP_CLASS
                        wo.ClassId == 11718 || // W_SLUMLORDVILLAEXPENSIVE_CLASS
                        wo.ClassId == 11719 || // W_SLUMLORDVILLAMODERATE_CLASS
                        wo.ClassId == 11977 || // W_SLUMLORDCOTTAGES349_579_CLASS
                        wo.ClassId == 11978 || // W_SLUMLORDVILLA851_925_CLASS
                        wo.ClassId == 11979 || // W_SLUMLORDCOTTAGE580_800_CLASS
                        wo.ClassId == 11980 || // W_SLUMLORDVILLA926_970_CLASS
                        wo.ClassId == 11980 || // W_SLUMLORDVILLA926_970_CLASS
                        wo.ClassId == 12461 || // W_SLUMLORDCOTTAGE1001_1075_CLASS
                        wo.ClassId == 12462 || // W_SLUMLORDCOTTAGE1076_1150_CLASS
                        wo.ClassId == 13078 || // W_SLUMLORDCOTTAGE1151_1275_CLASS
                        wo.ClassId == 13079 || // W_SLUMLORDCOTTAGE1276_1400_CLASS
                        wo.ClassId == 13080 || // W_SLUMLORDVILLA1401_1440_CLASS
                        wo.ClassId == 13081 || // W_SLUMLORDMANSION1441_1450_CLASS
                        wo.ClassId == 14243 || // W_SLUMLORDCOTTAGE1451_1650_CLASS
                        wo.ClassId == 14244 || // W_SLUMLORDCOTTAGE1651_1850_CLASS
                        wo.ClassId == 14245 || // W_SLUMLORDVILLA1851_1940_CLASS
                        wo.ClassId == 14246 || // W_SLUMLORDMANSION1941_1950_CLASS
                        wo.ClassId == 14247 || // W_SLUMLORDCOTTAGE1951_2150_CLASS
                        wo.ClassId == 14248 || // W_SLUMLORDCOTTAGE2151_2350_CLASS
                        wo.ClassId == 14249 || // W_SLUMLORDVILLA2351_2440_CLASS
                        wo.ClassId == 14250 || // W_SLUMLORDMANSION2441_2450_CLASS
                        wo.ClassId == 14934 || // W_SLUMLORDCOTTAGE2451_2525_CLASS
                        wo.ClassId == 14935 || // W_SLUMLORDCOTTAGE2526_2600_CLASS
                        wo.ClassId == 14936 || // W_SLUMLORDVILLA2601_2640_CLASS
                        wo.ClassId == 14937 || // W_SLUMLORDMANSION2641_2650_CLASS
                                                       // wo.ClassId == 15273 || // W_SLUMLORDFAKENUHMUDIRA_CLASS
                        wo.ClassId == 15608 || // W_SLUMLORDAPARTMENT_CLASS
                        wo.ClassId == 15609 || // W_SLUMLORDCOTTAGE2651_2725_CLASS
                        wo.ClassId == 15610 || // W_SLUMLORDCOTTAGE2726_2800_CLASS
                        wo.ClassId == 15611 || // W_SLUMLORDVILLA2801_2850_CLASS
                        wo.ClassId == 19074 || // W_SLUMLORDCOTTAGE3851_3925_CLASS
                        wo.ClassId == 19075 || // W_SLUMLORDCOTTAGE3926_4000_CLASS
                        wo.ClassId == 19076 || // W_SLUMLORDVILLA4001_4050_CLASS
                        wo.ClassId == 20850 || // W_SLUMLORDCOTTAGE6051_6125_CLASS
                        wo.ClassId == 20851 || // W_SLUMLORDCOTTAGE6126_6200_CLASS
                        wo.ClassId == 20852 || // W_SLUMLORDVILLA6201_6240_CLASS
                        wo.ClassId == 20853    // W_SLUMLORDMANSION6241_6250_CLASS
                                                       // wo.ClassId == 22118 || // W_SLUMLORDHAUNTEDMANSION_CLASS
                        )
                        wo.Type = (int)ACE.Entity.Enum.WeenieType.SlumLord;
                    else if (
                        wo.GetProperty(ACE.Entity.Enum.Properties.PropertyString.Name).Contains("Bolt")
                        || wo.GetProperty(ACE.Entity.Enum.Properties.PropertyString.Name).Contains("wave")
                        || wo.GetProperty(ACE.Entity.Enum.Properties.PropertyString.Name).Contains("Wave")
                        || wo.GetProperty(ACE.Entity.Enum.Properties.PropertyString.Name).Contains("Blast")
                        || wo.GetProperty(ACE.Entity.Enum.Properties.PropertyString.Name).Contains("Ring")
                        || wo.GetProperty(ACE.Entity.Enum.Properties.PropertyString.Name).Contains("Stream")
                        || wo.GetProperty(ACE.Entity.Enum.Properties.PropertyString.Name).Contains("Fist")
                        // || wo.GetProperty(ACE.Entity.Enum.Properties.PropertyString.Name).Contains("Missile")
                        // || wo.GetProperty(ACE.Entity.Enum.Properties.PropertyString.Name).Contains("Egg")
                        || wo.GetProperty(ACE.Entity.Enum.Properties.PropertyString.Name).Contains("Death")
                        || wo.GetProperty(ACE.Entity.Enum.Properties.PropertyString.Name).Contains("Fury")
                         || wo.GetProperty(ACE.Entity.Enum.Properties.PropertyString.Name).Contains("Wind")
                        || wo.GetProperty(ACE.Entity.Enum.Properties.PropertyString.Name).Contains("Flaming Skull")
                         || wo.GetProperty(ACE.Entity.Enum.Properties.PropertyString.Name).Contains("Edge")
                        // || wo.GetProperty(ACE.Entity.Enum.Properties.PropertyString.Name).Contains("Snowball")
                        || wo.GetProperty(ACE.Entity.Enum.Properties.PropertyString.Name).Contains("Bomb")
                        || wo.GetProperty(ACE.Entity.Enum.Properties.PropertyString.Name).Contains("Blade")
                        || wo.GetProperty(ACE.Entity.Enum.Properties.PropertyString.Name).Contains("Stalactite")
                        || wo.GetProperty(ACE.Entity.Enum.Properties.PropertyString.Name).Contains("Boulder")
                        || wo.GetProperty(ACE.Entity.Enum.Properties.PropertyString.Name).Contains("Whirlwind")
                        )
                        wo.Type = (int)ACE.Entity.Enum.WeenieType.ProjectileSpell;
                    else if (
                        wo.GetProperty(ACE.Entity.Enum.Properties.PropertyString.Name).Contains("Missile")
                        || wo.GetProperty(ACE.Entity.Enum.Properties.PropertyString.Name).Contains("Egg")
                        || wo.GetProperty(ACE.Entity.Enum.Properties.PropertyString.Name).Contains("Snowball")
                        )
                        wo.Type = (int)ACE.Entity.Enum.WeenieType.Missile;
                    else
                        goto default;
                    break;
                case ACE.Entity.Enum.ItemType.Creature:
                    var weenieHeaderFlag2 = (ACE.Entity.Enum.WeenieHeaderFlag2)(wo.GetProperty((ACE.Entity.Enum.Properties.PropertyDataId)8002) ?? (uint)ACE.Entity.Enum.WeenieHeaderFlag2.None);
                    if (weenieHeaderFlag2.HasFlag(ACE.Entity.Enum.WeenieHeaderFlag2.PetOwner))
                        if (wo.GetProperty(ACE.Entity.Enum.Properties.PropertyInt.RadarBlipColor).HasValue && wo.GetProperty(ACE.Entity.Enum.Properties.PropertyInt.RadarBlipColor) == (int)ACE.Entity.Enum.RadarColor.Yellow)
                            wo.Type = (int)ACE.Entity.Enum.WeenieType.Pet;
                        else
                            wo.Type = (int)ACE.Entity.Enum.WeenieType.CombatPet;
                    else if (
                        wo.GetProperty(ACE.Entity.Enum.Properties.PropertyString.Name).Contains("Pet")
                        || wo.GetProperty(ACE.Entity.Enum.Properties.PropertyString.Name).Contains("Wind-up")
                        || wo.ClassId == 48881
                        || wo.ClassId == 34902
                        || wo.ClassId == 48891
                        || wo.ClassId == 48879
                        || wo.ClassId == 34906
                        || wo.ClassId == 48887
                        || wo.ClassId == 48889
                        || wo.ClassId == 48883
                        || wo.ClassId == 34900
                        || wo.ClassId == 34901
                        || wo.ClassId == 34908
                        || wo.ClassId == 34898
                        )
                        wo.Type = (int)ACE.Entity.Enum.WeenieType.Pet;
                    else if (
                        wo.GetProperty(ACE.Entity.Enum.Properties.PropertyString.Name).Contains("Cow")
                        && !wo.GetProperty(ACE.Entity.Enum.Properties.PropertyString.Name).Contains("Auroch")
                        && !wo.GetProperty(ACE.Entity.Enum.Properties.PropertyString.Name).Contains("Snowman")
                        )
                        wo.Type = (int)ACE.Entity.Enum.WeenieType.Cow;
                    else if (
                        wo.ClassId >= 14342 && wo.ClassId <= 14347
                        || wo.ClassId >= 14404 && wo.ClassId <= 14409
                        )
                        wo.Type = (int)ACE.Entity.Enum.WeenieType.GamePiece;
                    else
                        wo.Type = (int)ACE.Entity.Enum.WeenieType.Creature;
                    break;
                case ACE.Entity.Enum.ItemType.Gameboard:
                    wo.Type = (int)ACE.Entity.Enum.WeenieType.Game;
                    break;
                case ACE.Entity.Enum.ItemType.Portal:
                    if (
                        wo.ClassId == 9620  || // W_PORTALHOUSE_CLASS
                        wo.ClassId == 10751 || // W_PORTALHOUSETEST_CLASS
                        wo.ClassId == 11730    // W_HOUSEPORTAL_CLASS
                        )
                        wo.Type = (int)ACE.Entity.Enum.WeenieType.HousePortal;
                    else
                        wo.Type = (int)ACE.Entity.Enum.WeenieType.Portal;
                    break;
                case ACE.Entity.Enum.ItemType.MeleeWeapon:
                    wo.Type = (int)ACE.Entity.Enum.WeenieType.MeleeWeapon;
                    break;
                case ACE.Entity.Enum.ItemType.MissileWeapon:
                    if (wo.GetProperty(ACE.Entity.Enum.Properties.PropertyInt.AmmoType).HasValue)
                        wo.Type = (int)ACE.Entity.Enum.WeenieType.MissileLauncher;
                    else
                        wo.Type = (int)ACE.Entity.Enum.WeenieType.Missile;
                    break;
                case ACE.Entity.Enum.ItemType.Money:
                    wo.Type = (int)ACE.Entity.Enum.WeenieType.Coin;
                    break;
                case ACE.Entity.Enum.ItemType.Gem:
                    if ((wo.GetProperty(ACE.Entity.Enum.Properties.PropertyInt.ItemUseable) ?? 0) == (int)ACE.Entity.Enum.Usable.SourceContainedTargetContained)
                        wo.Type = (int)ACE.Entity.Enum.WeenieType.CraftTool;
                    else
                        wo.Type = (int)ACE.Entity.Enum.WeenieType.Gem;
                    break;
                case ACE.Entity.Enum.ItemType.SpellComponents:
                    wo.Type = (int)ACE.Entity.Enum.WeenieType.SpellComponent;
                    break;
                case ACE.Entity.Enum.ItemType.ManaStone:
                    wo.Type = (int)ACE.Entity.Enum.WeenieType.ManaStone;
                    break;
                case ACE.Entity.Enum.ItemType.TinkeringTool:
                    wo.Type = (int)ACE.Entity.Enum.WeenieType.CraftTool;
                    break;
                case ACE.Entity.Enum.ItemType.Key:
                    wo.Type = (int)ACE.Entity.Enum.WeenieType.Key;
                    break;
                case ACE.Entity.Enum.ItemType.PromissoryNote:
                    wo.Type = (int)ACE.Entity.Enum.WeenieType.Stackable;
                    break;
                case ACE.Entity.Enum.ItemType.Writable:
                    wo.Type = (int)ACE.Entity.Enum.WeenieType.Scroll;
                    break;
                default:
                    if (wo.GetProperty(ACE.Entity.Enum.Properties.PropertyInt.MaxStructure).HasValue || wo.GetProperty(ACE.Entity.Enum.Properties.PropertyInt.TargetType).HasValue)
                        wo.Type = (int)ACE.Entity.Enum.WeenieType.CraftTool;
                    else if (wo.GetProperty(ACE.Entity.Enum.Properties.PropertyInt.MaxStackSize).HasValue)
                        wo.Type = (int)ACE.Entity.Enum.WeenieType.Stackable;
                    else if (wo.WeeniePropertiesSpellBook.Count > 0)
                        wo.Type = (int)ACE.Entity.Enum.WeenieType.Switch;
                    else
                        wo.Type = (int)ACE.Entity.Enum.WeenieType.Generic;
                    break;
            }

            return;
        }

        private ACE.Database.Models.World.Weenie CreateWeenieFromCreateObjectMsg(CM_Physics.CreateObject message)
        {
            var result = new ACE.Database.Models.World.Weenie();

            result.ClassId = message.wdesc._wcid;
            var className = "";
            if (Enum.IsDefined(typeof(WCLASSID), (int)message.wdesc._wcid))
                className = Enum.GetName(typeof(WCLASSID), message.wdesc._wcid).ToLower();
            else if (Enum.IsDefined(typeof(WeenieClasses), (ushort)message.wdesc._wcid))
            {
                var clsName = Enum.GetName(typeof(WeenieClasses), message.wdesc._wcid).ToLower().Substring(2);
                className = clsName.Substring(0,clsName.Length - 6);
            }
            else
                className = "ace" + message.wdesc._wcid.ToString() + "-" + message.wdesc._name.m_buffer.Replace("'", "").Replace(" ", "").Replace(".", "").Replace("(", "").Replace(")", "").Replace("+", "").Replace(":", "").Replace("_", "").Replace("-", "").Replace(",", "").Replace("\"", "").ToLower();

            result.ClassName = className.Replace("_", "-");

            result.WeeniePropertiesIID.Add(new ACE.Database.Models.World.WeeniePropertiesIID { Type = 8000, Value = message.object_id });

            result.WeeniePropertiesDID.Add(new ACE.Database.Models.World.WeeniePropertiesDID { Type = 8001, Value = message.wdesc.header });

            if ((message.wdesc._bitfield & (uint)CM_Physics.PublicWeenieDesc.BitfieldIndex.BF_INCLUDES_SECOND_HEADER) != 0)
                result.WeeniePropertiesDID.Add(new ACE.Database.Models.World.WeeniePropertiesDID { Type = 8002, Value = message.wdesc.header2 });

            result.WeeniePropertiesString.Add(new ACE.Database.Models.World.WeeniePropertiesString { Type = (int)STypeString.NAME_STRING, Value = message.wdesc._name.m_buffer });

            result.WeeniePropertiesDID.Add(new ACE.Database.Models.World.WeeniePropertiesDID { Type = (int)STypeDID.ICON_DID, Value = message.wdesc._iconID });

            result.WeeniePropertiesInt.Add(new ACE.Database.Models.World.WeeniePropertiesInt { Type = (int)STypeInt.ITEM_TYPE_INT, Value = (int)message.wdesc._type });

            result.WeeniePropertiesDID.Add(new ACE.Database.Models.World.WeeniePropertiesDID { Type = 8003, Value = message.wdesc._bitfield });

            if ((message.wdesc.header & (uint)CM_Physics.PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_PluralName) != 0)
                result.WeeniePropertiesString.Add(new ACE.Database.Models.World.WeeniePropertiesString { Type = (int)STypeString.PLURAL_NAME_STRING, Value = message.wdesc._plural_name.m_buffer });

            if ((message.wdesc.header & (uint)CM_Physics.PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_ItemsCapacity) != 0)
                result.WeeniePropertiesInt.Add(new ACE.Database.Models.World.WeeniePropertiesInt { Type = (int)STypeInt.ITEMS_CAPACITY_INT, Value = message.wdesc._itemsCapacity });

            if ((message.wdesc.header & (uint)CM_Physics.PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_ContainersCapacity) != 0)
                result.WeeniePropertiesInt.Add(new ACE.Database.Models.World.WeeniePropertiesInt { Type = (int)STypeInt.CONTAINERS_CAPACITY_INT, Value = message.wdesc._containersCapacity });

            if ((message.wdesc.header & (uint)CM_Physics.PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_AmmoType) != 0)
                result.WeeniePropertiesInt.Add(new ACE.Database.Models.World.WeeniePropertiesInt { Type = (int)STypeInt.AMMO_TYPE_INT, Value = (int)message.wdesc._ammoType });

            if ((message.wdesc.header & (uint)CM_Physics.PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_Value) != 0)
                result.WeeniePropertiesInt.Add(new ACE.Database.Models.World.WeeniePropertiesInt { Type = (int)STypeInt.VALUE_INT, Value = (int)message.wdesc._value });

            if ((message.wdesc.header & (uint)CM_Physics.PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_Useability) != 0)
                result.WeeniePropertiesInt.Add(new ACE.Database.Models.World.WeeniePropertiesInt { Type = (int)STypeInt.ITEM_USEABLE_INT, Value = (int)message.wdesc._useability });

            if ((message.wdesc.header & (uint)CM_Physics.PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_UseRadius) != 0)
                result.WeeniePropertiesFloat.Add(new ACE.Database.Models.World.WeeniePropertiesFloat { Type = (int)STypeFloat.USE_RADIUS_FLOAT, Value = message.wdesc._useRadius });

            if ((message.wdesc.header & (uint)CM_Physics.PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_TargetType) != 0)
                result.WeeniePropertiesInt.Add(new ACE.Database.Models.World.WeeniePropertiesInt { Type = (int)STypeInt.TARGET_TYPE_INT, Value = (int)message.wdesc._targetType });

            if ((message.wdesc.header & (uint)CM_Physics.PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_UIEffects) != 0)
                result.WeeniePropertiesInt.Add(new ACE.Database.Models.World.WeeniePropertiesInt { Type = (int)STypeInt.UI_EFFECTS_INT, Value = (int)message.wdesc._effects });

            if ((message.wdesc.header & (uint)CM_Physics.PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_CombatUse) != 0)
                result.WeeniePropertiesInt.Add(new ACE.Database.Models.World.WeeniePropertiesInt { Type = (int)STypeInt.COMBAT_USE_INT, Value = message.wdesc._combatUse });

            if ((message.wdesc.header & (uint)CM_Physics.PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_Structure) != 0)
                result.WeeniePropertiesInt.Add(new ACE.Database.Models.World.WeeniePropertiesInt { Type = (int)STypeInt.STRUCTURE_INT, Value = (int)message.wdesc._structure });

            if ((message.wdesc.header & (uint)CM_Physics.PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_MaxStructure) != 0)
                result.WeeniePropertiesInt.Add(new ACE.Database.Models.World.WeeniePropertiesInt { Type = (int)STypeInt.MAX_STRUCTURE_INT, Value = (int)message.wdesc._maxStructure });

            if ((message.wdesc.header & (uint)CM_Physics.PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_StackSize) != 0)
                result.WeeniePropertiesInt.Add(new ACE.Database.Models.World.WeeniePropertiesInt { Type = (int)STypeInt.STACK_SIZE_INT, Value = (int)message.wdesc._stackSize });

            if ((message.wdesc.header & (uint)CM_Physics.PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_MaxStackSize) != 0)
                result.WeeniePropertiesInt.Add(new ACE.Database.Models.World.WeeniePropertiesInt { Type = (int)STypeInt.MAX_STACK_SIZE_INT, Value = (int)message.wdesc._maxStackSize });

            if ((message.wdesc.header & (uint)CM_Physics.PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_ContainerID) != 0)
                result.WeeniePropertiesIID.Add(new ACE.Database.Models.World.WeeniePropertiesIID { Type = (int)STypeIID.CONTAINER_IID, Value = message.wdesc._containerID });

            if ((message.wdesc.header & (uint)CM_Physics.PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_WielderID) != 0)
                result.WeeniePropertiesIID.Add(new ACE.Database.Models.World.WeeniePropertiesIID { Type = (int)STypeIID.WIELDER_IID, Value = message.wdesc._wielderID });

            if ((message.wdesc.header & (uint)CM_Physics.PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_ValidLocations) != 0)
                result.WeeniePropertiesInt.Add(new ACE.Database.Models.World.WeeniePropertiesInt { Type = (int)STypeInt.LOCATIONS_INT, Value = (int)message.wdesc._valid_locations });

            if ((message.wdesc.header & (uint)CM_Physics.PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_Location) != 0)
                result.WeeniePropertiesInt.Add(new ACE.Database.Models.World.WeeniePropertiesInt { Type = (int)STypeInt.CURRENT_WIELDED_LOCATION_INT, Value = (int)message.wdesc._location });

            if ((message.wdesc.header & (uint)CM_Physics.PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_Priority) != 0)
                result.WeeniePropertiesInt.Add(new ACE.Database.Models.World.WeeniePropertiesInt { Type = (int)STypeInt.CLOTHING_PRIORITY_INT, Value = (int)message.wdesc._priority });

            if ((message.wdesc.header & (uint)CM_Physics.PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_BlipColor) != 0)
                result.WeeniePropertiesInt.Add(new ACE.Database.Models.World.WeeniePropertiesInt { Type = (int)STypeInt.RADARBLIP_COLOR_INT, Value = (int)message.wdesc._blipColor });

            if ((message.wdesc.header & (uint)CM_Physics.PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_RadarEnum) != 0)
                result.WeeniePropertiesInt.Add(new ACE.Database.Models.World.WeeniePropertiesInt { Type = (int)STypeInt.SHOWABLE_ON_RADAR_INT, Value = (int)message.wdesc._radar_enum });

            if ((message.wdesc.header & (uint)CM_Physics.PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_PScript) != 0)
                result.WeeniePropertiesDID.Add(new ACE.Database.Models.World.WeeniePropertiesDID { Type = (int)STypeDID.PHYSICS_SCRIPT_DID, Value = message.wdesc._pscript });

            if ((message.wdesc.header & (uint)CM_Physics.PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_Workmanship) != 0)
                result.WeeniePropertiesFloat.Add(new ACE.Database.Models.World.WeeniePropertiesFloat { Type = 8004, Value = message.wdesc._workmanship });

            if ((message.wdesc.header & (uint)CM_Physics.PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_Burden) != 0)
                result.WeeniePropertiesInt.Add(new ACE.Database.Models.World.WeeniePropertiesInt { Type = (int)STypeInt.ENCUMB_VAL_INT, Value = (int)message.wdesc._burden });

            if ((message.wdesc.header & (uint)CM_Physics.PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_SpellID) != 0)
                result.WeeniePropertiesDID.Add(new ACE.Database.Models.World.WeeniePropertiesDID { Type = (int)STypeDID.SPELL_DID, Value = message.wdesc._spellID });

            if ((message.wdesc.header & (uint)CM_Physics.PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_HouseOwner) != 0)
                result.WeeniePropertiesIID.Add(new ACE.Database.Models.World.WeeniePropertiesIID { Type = (int)STypeIID.HOUSE_OWNER_IID, Value = message.wdesc._house_owner_iid });

            //if ((message.wdesc.header & (uint)CM_Physics.PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_HouseRestrictions) != 0)
            //    result.WeeniePropertiesIID.Add(new ACE.Database.Models.World.WeeniePropertiesIID { Type = (int)STypeIID.WIELDER_IID, Value = message.wdesc._wielderID });

            if ((message.wdesc.header & (uint)CM_Physics.PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_HookItemTypes) != 0)
                result.WeeniePropertiesInt.Add(new ACE.Database.Models.World.WeeniePropertiesInt { Type = (int)STypeInt.HOOK_ITEM_TYPE_INT, Value = (int)message.wdesc._hook_item_types });

            if ((message.wdesc.header & (uint)CM_Physics.PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_Monarch) != 0)
                result.WeeniePropertiesIID.Add(new ACE.Database.Models.World.WeeniePropertiesIID { Type = (int)STypeIID.MONARCH_IID, Value = message.wdesc._monarch });

            if ((message.wdesc.header & (uint)CM_Physics.PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_HookType) != 0)
                result.WeeniePropertiesInt.Add(new ACE.Database.Models.World.WeeniePropertiesInt { Type = (int)STypeInt.HOOK_TYPE_INT, Value = (int)message.wdesc._hook_type });

            if ((message.wdesc.header & (uint)CM_Physics.PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_IconOverlay) != 0)
                result.WeeniePropertiesDID.Add(new ACE.Database.Models.World.WeeniePropertiesDID { Type = (int)STypeDID.ICON_OVERLAY_DID, Value = message.wdesc._iconOverlayID });

            if ((message.wdesc.header2 & (uint)CM_Physics.PublicWeenieDesc.PublicWeenieDescPackHeader2.PWD2_Packed_IconUnderlay) != 0)
                result.WeeniePropertiesDID.Add(new ACE.Database.Models.World.WeeniePropertiesDID { Type = (int)STypeDID.ICON_UNDERLAY_DID, Value = message.wdesc._iconUnderlayID });

            if ((message.wdesc.header & unchecked((uint)CM_Physics.PublicWeenieDesc.PublicWeenieDescPackHeader.PWD_Packed_MaterialType)) != 0)
                result.WeeniePropertiesInt.Add(new ACE.Database.Models.World.WeeniePropertiesInt { Type = (int)STypeInt.MATERIAL_TYPE_INT, Value = (int)message.wdesc._material_type });

            if ((message.wdesc.header2 & (uint)CM_Physics.PublicWeenieDesc.PublicWeenieDescPackHeader2.PWD2_Packed_CooldownID) != 0)
                result.WeeniePropertiesInt.Add(new ACE.Database.Models.World.WeeniePropertiesInt { Type = (int)STypeInt.SHARED_COOLDOWN_INT, Value = (int)message.wdesc._cooldown_id });

            if ((message.wdesc.header2 & (uint)CM_Physics.PublicWeenieDesc.PublicWeenieDescPackHeader2.PWD2_Packed_CooldownDuration) != 0)
                result.WeeniePropertiesFloat.Add(new ACE.Database.Models.World.WeeniePropertiesFloat { Type = (int)STypeFloat.COOLDOWN_DURATION_FLOAT, Value = message.wdesc._cooldown_duration });

            if ((message.wdesc.header2 & (uint)CM_Physics.PublicWeenieDesc.PublicWeenieDescPackHeader2.PWD2_Packed_PetOwner) != 0)
                result.WeeniePropertiesIID.Add(new ACE.Database.Models.World.WeeniePropertiesIID { Type = (int)STypeIID.PET_OWNER_IID, Value = message.wdesc._pet_owner });

            if ((message.wdesc._bitfield & (uint)CM_Physics.PublicWeenieDesc.BitfieldIndex.BF_ADMIN) != 0)
                result.WeeniePropertiesBool.Add(new ACE.Database.Models.World.WeeniePropertiesBool { Type = (int)STypeBool.IS_ADMIN_BOOL, Value = true });
            //if ((message.wdesc._bitfield & (uint)CM_Physics.PublicWeenieDesc.BitfieldIndex.BF_ATTACKABLE) != 0)
            //    result.WeeniePropertiesBool.Add(new ACE.Database.Models.World.WeeniePropertiesBool { Type = (int)STypeBool.ATTACKABLE_BOOL, Value = true });
            //else
            if ((message.wdesc._bitfield & (uint)CM_Physics.PublicWeenieDesc.BitfieldIndex.BF_ATTACKABLE) == 0)
                result.WeeniePropertiesBool.Add(new ACE.Database.Models.World.WeeniePropertiesBool { Type = (int)STypeBool.ATTACKABLE_BOOL, Value = false });
            if ((message.wdesc._bitfield & (uint)CM_Physics.PublicWeenieDesc.BitfieldIndex.BF_HIDDEN_ADMIN) != 0)
                result.WeeniePropertiesBool.Add(new ACE.Database.Models.World.WeeniePropertiesBool { Type = (int)STypeBool.HIDDEN_ADMIN_BOOL, Value = true });
            if ((message.wdesc._bitfield & (uint)CM_Physics.PublicWeenieDesc.BitfieldIndex.BF_IMMUNE_CELL_RESTRICTIONS) != 0)
                result.WeeniePropertiesBool.Add(new ACE.Database.Models.World.WeeniePropertiesBool { Type = (int)STypeBool.IGNORE_HOUSE_BARRIERS_BOOL, Value = true });
            if ((message.wdesc._bitfield & (uint)CM_Physics.PublicWeenieDesc.BitfieldIndex.BF_INSCRIBABLE) != 0)
                result.WeeniePropertiesBool.Add(new ACE.Database.Models.World.WeeniePropertiesBool { Type = (int)STypeBool.INSCRIBABLE_BOOL, Value = true });
            if ((message.wdesc._bitfield & (uint)CM_Physics.PublicWeenieDesc.BitfieldIndex.BF_REQUIRES_PACKSLOT) != 0)
                result.WeeniePropertiesBool.Add(new ACE.Database.Models.World.WeeniePropertiesBool { Type = (int)STypeBool.REQUIRES_BACKPACK_SLOT_BOOL, Value = true });
            if ((message.wdesc._bitfield & (uint)CM_Physics.PublicWeenieDesc.BitfieldIndex.BF_RETAINED) != 0)
                result.WeeniePropertiesBool.Add(new ACE.Database.Models.World.WeeniePropertiesBool { Type = (int)STypeBool.RETAINED_BOOL, Value = true });
            if ((message.wdesc._bitfield & (uint)CM_Physics.PublicWeenieDesc.BitfieldIndex.BF_STUCK) != 0)
                result.WeeniePropertiesBool.Add(new ACE.Database.Models.World.WeeniePropertiesBool { Type = (int)STypeBool.STUCK_BOOL, Value = true });
            //else
            //    result.WeeniePropertiesBool.Add(new ACE.Database.Models.World.WeeniePropertiesBool { Type = (int)STypeBool.STUCK_BOOL, Value = false });
            if ((message.wdesc._bitfield & (uint)CM_Physics.PublicWeenieDesc.BitfieldIndex.BF_UI_HIDDEN) != 0)
                result.WeeniePropertiesBool.Add(new ACE.Database.Models.World.WeeniePropertiesBool { Type = (int)STypeBool.UI_HIDDEN_BOOL, Value = true });
            if ((message.wdesc._bitfield & (uint)CM_Physics.PublicWeenieDesc.BitfieldIndex.BF_WIELD_LEFT) != 0)
                result.WeeniePropertiesBool.Add(new ACE.Database.Models.World.WeeniePropertiesBool { Type = (int)STypeBool.AUTOWIELD_LEFT_BOOL, Value = true });
            if ((message.wdesc._bitfield & (uint)CM_Physics.PublicWeenieDesc.BitfieldIndex.BF_WIELD_ON_USE) != 0)
                result.WeeniePropertiesBool.Add(new ACE.Database.Models.World.WeeniePropertiesBool { Type = (int)STypeBool.WIELD_ON_USE_BOOL, Value = true });

            if (message.objdesc.subpalettes.Count > 0)
            {
                result.WeeniePropertiesDID.Add(new ACE.Database.Models.World.WeeniePropertiesDID { Type = (int)STypeDID.PALETTE_BASE_DID, Value = message.objdesc.paletteID });

                foreach (var subpalette in message.objdesc.subpalettes)
                {
                    result.WeeniePropertiesPalette.Add(new ACE.Database.Models.World.WeeniePropertiesPalette { SubPaletteId = subpalette.subID, Offset = (ushort)subpalette.offset, Length = (ushort)subpalette.numcolors });
                }
            }

            if (message.objdesc.tmChanges.Count > 0)
            {
                foreach (var texture in message.objdesc.tmChanges)
                {
                    result.WeeniePropertiesTextureMap.Add(new ACE.Database.Models.World.WeeniePropertiesTextureMap { Index = texture.part_index, OldId = texture.old_tex_id, NewId = texture.new_tex_id });
                }
            }

            if (message.objdesc.apChanges.Count > 0)
            {
                foreach (var animPart in message.objdesc.apChanges)
                {
                    result.WeeniePropertiesAnimPart.Add(new ACE.Database.Models.World.WeeniePropertiesAnimPart { Index = animPart.part_index, AnimationId = animPart.part_id });
                }
            }

            result.WeeniePropertiesDID.Add(new ACE.Database.Models.World.WeeniePropertiesDID { Type = 8005, Value = message.physicsdesc.bitfield });

            result.WeeniePropertiesInt.Add(new ACE.Database.Models.World.WeeniePropertiesInt { Type = (int)STypeInt.PHYSICS_STATE_INT, Value = (int)message.physicsdesc.state });

            if ((message.physicsdesc.bitfield & (uint)CM_Physics.PhysicsDesc.PhysicsDescInfo.MOVEMENT) != 0)
            {
                result.WeeniePropertiesString.Add(new ACE.Database.Models.World.WeeniePropertiesString { Type = 8006, Value = ConvertMovementBufferToString(message.physicsdesc.CMS) });
                result.WeeniePropertiesInt.Add(new ACE.Database.Models.World.WeeniePropertiesInt { Type = 8007, Value = message.physicsdesc.autonomous_movement });
            }

            if ((message.physicsdesc.bitfield & (uint)CM_Physics.PhysicsDesc.PhysicsDescInfo.ANIMFRAME_ID) != 0)
                //result.WeeniePropertiesInt.Add(new ACE.Database.Models.World.WeeniePropertiesInt { Type = (int)STypeInt.PLACEMENT_INT, Value = (int)message.physicsdesc.animframe_id });
                result.WeeniePropertiesInt.Add(new ACE.Database.Models.World.WeeniePropertiesInt { Type = 8041, Value = (int)message.physicsdesc.animframe_id });

            if ((message.physicsdesc.bitfield & (uint)CM_Physics.PhysicsDesc.PhysicsDescInfo.POSITION) != 0)
                result.WeeniePropertiesPosition.Add(
                    new ACE.Database.Models.World.WeeniePropertiesPosition
                    {
                        PositionType = 8040,
                        ObjCellId = message.physicsdesc.pos.objcell_id,
                        OriginX = message.physicsdesc.pos.frame.m_fOrigin.x,
                        OriginY = message.physicsdesc.pos.frame.m_fOrigin.y,
                        OriginZ = message.physicsdesc.pos.frame.m_fOrigin.z,
                        AnglesW = message.physicsdesc.pos.frame.qw,
                        AnglesX = message.physicsdesc.pos.frame.qx,
                        AnglesY = message.physicsdesc.pos.frame.qy,
                        AnglesZ = message.physicsdesc.pos.frame.qz
                    });

            if ((message.physicsdesc.bitfield & (uint)CM_Physics.PhysicsDesc.PhysicsDescInfo.MTABLE) != 0)
                result.WeeniePropertiesDID.Add(new ACE.Database.Models.World.WeeniePropertiesDID { Type = (int)STypeDID.MOTION_TABLE_DID, Value = message.physicsdesc.mtable_id });

            if ((message.physicsdesc.bitfield & (uint)CM_Physics.PhysicsDesc.PhysicsDescInfo.STABLE) != 0)
                result.WeeniePropertiesDID.Add(new ACE.Database.Models.World.WeeniePropertiesDID { Type = (int)STypeDID.SOUND_TABLE_DID, Value = message.physicsdesc.stable_id });

            if ((message.physicsdesc.bitfield & (uint)CM_Physics.PhysicsDesc.PhysicsDescInfo.PETABLE) != 0)
                result.WeeniePropertiesDID.Add(new ACE.Database.Models.World.WeeniePropertiesDID { Type = (int)STypeDID.PHYSICS_EFFECT_TABLE_DID, Value = message.physicsdesc.phstable_id });

            if ((message.physicsdesc.bitfield & (uint)CM_Physics.PhysicsDesc.PhysicsDescInfo.CSetup) != 0)
                result.WeeniePropertiesDID.Add(new ACE.Database.Models.World.WeeniePropertiesDID { Type = (int)STypeDID.SETUP_DID, Value = message.physicsdesc.setup_id });

            if ((message.physicsdesc.bitfield & (uint)CM_Physics.PhysicsDesc.PhysicsDescInfo.PARENT) != 0)
            {
                result.WeeniePropertiesIID.Add(new ACE.Database.Models.World.WeeniePropertiesIID { Type = 8008, Value = message.physicsdesc.parent_id });
                result.WeeniePropertiesDID.Add(new ACE.Database.Models.World.WeeniePropertiesDID { Type = 8009, Value = message.physicsdesc.location_id });
            }

            if ((message.physicsdesc.bitfield & (uint)CM_Physics.PhysicsDesc.PhysicsDescInfo.CHILDREN) != 0)
            {
                //result.WeeniePropertiesIID.Add(new ACE.Database.Models.World.WeeniePropertiesIID { Type = 8008, Value = message.physicsdesc.parent_id });
                //result.WeeniePropertiesDID.Add(new ACE.Database.Models.World.WeeniePropertiesDID { Type = 8009, Value = message.physicsdesc.location_id });
            }

            if ((message.physicsdesc.bitfield & (uint)CM_Physics.PhysicsDesc.PhysicsDescInfo.OBJSCALE) != 0)
                result.WeeniePropertiesFloat.Add(new ACE.Database.Models.World.WeeniePropertiesFloat { Type = (int)STypeFloat.DEFAULT_SCALE_FLOAT, Value = message.physicsdesc.object_scale });

            if ((message.physicsdesc.bitfield & (uint)CM_Physics.PhysicsDesc.PhysicsDescInfo.FRICTION) != 0)
                result.WeeniePropertiesFloat.Add(new ACE.Database.Models.World.WeeniePropertiesFloat { Type = (int)STypeFloat.FRICTION_FLOAT, Value = message.physicsdesc.friction });

            if ((message.physicsdesc.bitfield & (uint)CM_Physics.PhysicsDesc.PhysicsDescInfo.ELASTICITY) != 0)
                result.WeeniePropertiesFloat.Add(new ACE.Database.Models.World.WeeniePropertiesFloat { Type = (int)STypeFloat.ELASTICITY_FLOAT, Value = message.physicsdesc.elasticity });

            if ((message.physicsdesc.bitfield & (uint)CM_Physics.PhysicsDesc.PhysicsDescInfo.TRANSLUCENCY) != 0)
                result.WeeniePropertiesFloat.Add(new ACE.Database.Models.World.WeeniePropertiesFloat { Type = (int)STypeFloat.TRANSLUCENCY_FLOAT, Value = message.physicsdesc.translucency });

            if ((message.physicsdesc.bitfield & (uint)CM_Physics.PhysicsDesc.PhysicsDescInfo.VELOCITY) != 0)
            {
                result.WeeniePropertiesFloat.Add(new ACE.Database.Models.World.WeeniePropertiesFloat { Type = 8010, Value = message.physicsdesc.velocity.x });
                result.WeeniePropertiesFloat.Add(new ACE.Database.Models.World.WeeniePropertiesFloat { Type = 8011, Value = message.physicsdesc.velocity.y });
                result.WeeniePropertiesFloat.Add(new ACE.Database.Models.World.WeeniePropertiesFloat { Type = 8012, Value = message.physicsdesc.velocity.z });
            }

            if ((message.physicsdesc.bitfield & (uint)CM_Physics.PhysicsDesc.PhysicsDescInfo.ACCELERATION) != 0)
            {
                result.WeeniePropertiesFloat.Add(new ACE.Database.Models.World.WeeniePropertiesFloat { Type = 8013, Value = message.physicsdesc.acceleration.x });
                result.WeeniePropertiesFloat.Add(new ACE.Database.Models.World.WeeniePropertiesFloat { Type = 8014, Value = message.physicsdesc.acceleration.y });
                result.WeeniePropertiesFloat.Add(new ACE.Database.Models.World.WeeniePropertiesFloat { Type = 8015, Value = message.physicsdesc.acceleration.z });
            }

            if ((message.physicsdesc.bitfield & (uint)CM_Physics.PhysicsDesc.PhysicsDescInfo.OMEGA) != 0)
            {
                result.WeeniePropertiesFloat.Add(new ACE.Database.Models.World.WeeniePropertiesFloat { Type = 8016, Value = message.physicsdesc.omega.x });
                result.WeeniePropertiesFloat.Add(new ACE.Database.Models.World.WeeniePropertiesFloat { Type = 8017, Value = message.physicsdesc.omega.y });
                result.WeeniePropertiesFloat.Add(new ACE.Database.Models.World.WeeniePropertiesFloat { Type = 8018, Value = message.physicsdesc.omega.z });
            }

            if ((message.physicsdesc.bitfield & (uint)CM_Physics.PhysicsDesc.PhysicsDescInfo.DEFAULT_SCRIPT) != 0)
                //result.WeeniePropertiesDID.Add(new ACE.Database.Models.World.WeeniePropertiesDID { Type = 8019, Value = (uint)message.physicsdesc.default_script });
                result.WeeniePropertiesDID.Add(new ACE.Database.Models.World.WeeniePropertiesDID { Type = (int)STypeDID.PHYSICS_SCRIPT_DID, Value = (uint)message.physicsdesc.default_script });

            if ((message.physicsdesc.bitfield & (uint)CM_Physics.PhysicsDesc.PhysicsDescInfo.DEFAULT_SCRIPT_INTENSITY) != 0)
                result.WeeniePropertiesFloat.Add(new ACE.Database.Models.World.WeeniePropertiesFloat { Type = (int)STypeFloat.PHYSICS_SCRIPT_INTENSITY_FLOAT, Value = message.physicsdesc.default_script_intensity });

            if ((message.physicsdesc.bitfield & (uint)CM_Physics.PhysicsDesc.PhysicsDescInfo.TIMESTAMPS) != 0)
            {
                for (int i = 0; i < message.physicsdesc.timestamps.Length; ++i)
                {
                    result.WeeniePropertiesDID.Add(new ACE.Database.Models.World.WeeniePropertiesDID { Type = (ushort)(8020 + i), Value = message.physicsdesc.timestamps[i - 1] });
                }
            }

            //if ((message.physicsdesc.state & (uint)PhysicsState.STATIC_PS) != 0)
            //    result.WeeniePropertiesBool.Add(new ACE.Database.Models.World.WeeniePropertiesBool { Type = (int)STypeBool.STUCK_BOOL, Value = true });
            //if ((message.physicsdesc.state & (uint)PhysicsState.ETHEREAL_PS) != 0)
            //    result.WeeniePropertiesBool.Add(new ACE.Database.Models.World.WeeniePropertiesBool { Type = (int)STypeBool.ETHEREAL_BOOL, Value = true });
            //else
            //    result.WeeniePropertiesBool.Add(new ACE.Database.Models.World.WeeniePropertiesBool { Type = (int)STypeBool.ETHEREAL_BOOL, Value = false });
            //if ((message.physicsdesc.state & (uint)PhysicsState.REPORT_COLLISIONS_PS) != 0)
            //    result.WeeniePropertiesBool.Add(new ACE.Database.Models.World.WeeniePropertiesBool { Type = (int)STypeBool.REPORT_COLLISIONS_BOOL, Value = true });
            //if ((message.physicsdesc.state & (uint)PhysicsState.IGNORE_COLLISIONS_PS) != 0)
            //    result.WeeniePropertiesBool.Add(new ACE.Database.Models.World.WeeniePropertiesBool { Type = (int)STypeBool.IGNORE_COLLISIONS_BOOL, Value = true });
            //if ((message.physicsdesc.state & (uint)PhysicsState.NODRAW_PS) != 0)
            //    result.WeeniePropertiesBool.Add(new ACE.Database.Models.World.WeeniePropertiesBool { Type = (int)STypeBool.NODRAW_BOOL, Value = true });
            //if ((message.physicsdesc.state & (uint)PhysicsState.GRAVITY_PS) != 0)
            //    result.WeeniePropertiesBool.Add(new ACE.Database.Models.World.WeeniePropertiesBool { Type = (int)STypeBool.GRAVITY_STATUS_BOOL, Value = true });
            //if ((message.physicsdesc.state & (uint)PhysicsState.LIGHTING_ON_PS) != 0)
            //    result.WeeniePropertiesBool.Add(new ACE.Database.Models.World.WeeniePropertiesBool { Type = (int)STypeBool.LIGHTS_STATUS_BOOL, Value = true });
            //////if ((message.physicsdesc.state & (uint)PhysicsState.HIDDEN_PS) != 0)
            //////    result.WeeniePropertiesBool.Add(new ACE.Database.Models.World.WeeniePropertiesBool { Type = (int)STypeBool.VISIBILITY_BOOL, Value = true });
            //if ((message.physicsdesc.state & (uint)PhysicsState.SCRIPTED_COLLISION_PS) != 0)
            //    result.WeeniePropertiesBool.Add(new ACE.Database.Models.World.WeeniePropertiesBool { Type = (int)STypeBool.SCRIPTED_COLLISION_BOOL, Value = true });
            //if ((message.physicsdesc.state & (uint)PhysicsState.INELASTIC_PS) != 0)
            //    result.WeeniePropertiesBool.Add(new ACE.Database.Models.World.WeeniePropertiesBool { Type = (int)STypeBool.INELASTIC_BOOL, Value = true });
            //////if ((message.physicsdesc.state & (uint)PhysicsState.CLOAKED_PS) != 0)
            //////    result.WeeniePropertiesBool.Add(new ACE.Database.Models.World.WeeniePropertiesBool { Type = (int)STypeBool.HIDDEN_ADMIN_BOOL, Value = true });
            //if ((message.physicsdesc.state & (uint)PhysicsState.REPORT_COLLISIONS_AS_ENVIRONMENT_PS) != 0)
            //    result.WeeniePropertiesBool.Add(new ACE.Database.Models.World.WeeniePropertiesBool { Type = (int)STypeBool.REPORT_COLLISIONS_AS_ENVIRONMENT_BOOL, Value = true });
            //if ((message.physicsdesc.state & (uint)PhysicsState.EDGE_SLIDE_PS) != 0)
            //    result.WeeniePropertiesBool.Add(new ACE.Database.Models.World.WeeniePropertiesBool { Type = (int)STypeBool.ALLOW_EDGE_SLIDE_BOOL, Value = true });
            //if ((message.physicsdesc.state & (uint)PhysicsState.FROZEN_PS) != 0)
            //    result.WeeniePropertiesBool.Add(new ACE.Database.Models.World.WeeniePropertiesBool { Type = (int)STypeBool.IS_FROZEN_BOOL, Value = true });

            return result;
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

            if (newPosition?.objcell_id == 0)
                return true; // can't dedupe this

            return positions.Any(p => p.objcell_id == newPosition.objcell_id
                                && Math.Abs(p.frame.m_fOrigin.x - newPosition.frame.m_fOrigin.x) < margin
                                && Math.Abs(p.frame.m_fOrigin.y - newPosition.frame.m_fOrigin.y) < margin
                                && Math.Abs(p.frame.m_fOrigin.z - newPosition.frame.m_fOrigin.z) < margin);
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            toolStripStatusLabel1.Text = "Files Processed: " + filesProcessed.ToString("N0") + " of " + filesToProcess.Count.ToString("N0");

            toolStripStatusLabel2.Text = "Fragments Processed: " + fragmentsProcessed.ToString("N0");

            toolStripStatusLabel3.Text = "Total Hits: " + totalHits.ToString("N0");

            toolStripStatusLabel4.Text = "Frag Exceptions: " + totalExceptions.ToString("N0");
        }

        public static string CleanForJSON(string s)
        {
            if (s == null || s.Length == 0)
            {
                return "";
            }

            char c = '\0';
            int i;
            int len = s.Length;
            StringBuilder sb = new StringBuilder(len + 4);
            String t;

            for (i = 0; i < len; i += 1)
            {
                c = s[i];
                switch (c)
                {
                    case '\\':
                    case '"':
                        sb.Append('\\');
                        sb.Append(c);
                        break;
                    case '/':
                        sb.Append('\\');
                        sb.Append(c);
                        break;
                    case '\b':
                        sb.Append("\\b");
                        break;
                    case '\t':
                        sb.Append("\\t");
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\f':
                        sb.Append("\\f");
                        break;
                    case '\r':
                        sb.Append("\\r");
                        break;
                    default:
                        if (c < ' ')
                        {
                            t = "000" + String.Format("X", c);
                            sb.Append("\\u" + t.Substring(t.Length - 4));
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            return sb.ToString();
        }

        // https://github.com/ACEmulator/ACE/blob/master/Source/ACE.Entity/Enum/Properties/PropertyInt.cs
        public enum IntValueKey
        {
            // properties marked as ServerOnly are properties we never saw in PCAPs, from here:
            // http://ac.yotesfan.com/ace_object/not_used_enums.php
            // source: @OptimShi
            // description attributes are used by the weenie editor for a cleaner display name

            Undef = 0,
            [ServerOnly]
            ItemType = 1,
            CreatureType = 2,
            [ServerOnly]
            PaletteTemplate = 3,
            ClothingPriority = 4,
            [SendOnLogin]
            EncumbranceVal = 5, // ENCUMB_VAL_INT,
            [SendOnLogin]
            ItemsCapacity = 6,
            [SendOnLogin]
            ContainersCapacity = 7,
            [ServerOnly]
            Mass = 8,
            [ServerOnly]
            ValidLocations = 9, // LOCATIONS_INT
            [ServerOnly]
            CurrentWieldedLocation = 10,
            [ServerOnly]
            MaxStackSize = 11,
            [ServerOnly]
            StackSize = 12,
            [ServerOnly]
            StackUnitEncumbrance = 13,
            [ServerOnly]
            StackUnitMass = 14,
            [ServerOnly]
            StackUnitValue = 15,
            [ServerOnly]
            ItemUseable = 16,
            RareId = 17,
            [ServerOnly]
            UiEffects = 18,
            Value = 19,
            [Ephemeral]
            [SendOnLogin]
            CoinValue = 20,
            TotalExperience = 21,
            AvailableCharacter = 22,
            TotalSkillCredits = 23,
            [SendOnLogin]
            AvailableSkillCredits = 24,
            [SendOnLogin]
            Level = 25,
            AccountRequirements = 26,
            ArmorType = 27,
            ArmorLevel = 28,
            AllegianceCpPool = 29,
            [SendOnLogin]
            AllegianceRank = 30,
            ChannelsAllowed = 31,
            ChannelsActive = 32,
            Bonded = 33,
            MonarchsRank = 34,
            AllegianceFollowers = 35,
            ResistMagic = 36,
            ResistItemAppraisal = 37,
            ResistLockpick = 38,
            DeprecatedResistRepair = 39,
            [SendOnLogin]
            CombatMode = 40,
            CurrentAttackHeight = 41,
            CombatCollisions = 42,
            [SendOnLogin]
            NumDeaths = 43,
            Damage = 44,
            DamageType = 45,
            [ServerOnly]
            DefaultCombatStyle = 46,
            [SendOnLogin]
            AttackType = 47,
            WeaponSkill = 48,
            WeaponTime = 49,
            AmmoType = 50,
            CombatUse = 51,
            [ServerOnly]
            ParentLocation = 52,
            /// <summary>
            /// TODO: Migrate inventory order away from this and instead use the new InventoryOrder property
            /// TODO: PlacementPosition is used (very sparingly) in cache.bin, so it has (or had) a meaning at one point before we hijacked it
            /// TODO: and used it for our own inventory order
            /// </summary>
            [ServerOnly]
            PlacementPosition = 53,
            WeaponEncumbrance = 54,
            WeaponMass = 55,
            ShieldValue = 56,
            ShieldEncumbrance = 57,
            MissileInventoryLocation = 58,
            FullDamageType = 59,
            WeaponRange = 60,
            AttackersSkill = 61,
            DefendersSkill = 62,
            AttackersSkillValue = 63,
            AttackersClass = 64,
            [ServerOnly]
            Placement = 65,
            CheckpointStatus = 66,
            Tolerance = 67,
            TargetingTactic = 68,
            CombatTactic = 69,
            HomesickTargetingTactic = 70,
            NumFollowFailures = 71,
            FriendType = 72,
            FoeType = 73,
            MerchandiseItemTypes = 74,
            MerchandiseMinValue = 75,
            MerchandiseMaxValue = 76,
            NumItemsSold = 77,
            NumItemsBought = 78,
            MoneyIncome = 79,
            MoneyOutflow = 80,
            [Ephemeral]
            MaxGeneratedObjects = 81,
            [Ephemeral]
            InitGeneratedObjects = 82,
            ActivationResponse = 83,
            OriginalValue = 84,
            NumMoveFailures = 85,
            MinLevel = 86,
            MaxLevel = 87,
            LockpickMod = 88,
            BoosterEnum = 89,
            BoostValue = 90,
            MaxStructure = 91,
            Structure = 92,
            [ServerOnly]
            PhysicsState = 93,
            [ServerOnly]
            TargetType = 94,
            RadarBlipColor = 95,
            EncumbranceCapacity = 96,
            LoginTimestamp = 97,
            [SendOnLogin]
            CreationTimestamp = 98,
            PkLevelModifier = 99,
            GeneratorType = 100,
            AiAllowedCombatStyle = 101,
            LogoffTimestamp = 102,
            GeneratorDestructionType = 103,
            ActivationCreateClass = 104,
            ItemWorkmanship = 105,
            ItemSpellcraft = 106,
            ItemCurMana = 107,
            ItemMaxMana = 108,
            ItemDifficulty = 109,
            ItemAllegianceRankLimit = 110,
            PortalBitmask = 111,
            AdvocateLevel = 112,
            [SendOnLogin]
            Gender = 113,
            Attuned = 114,
            ItemSkillLevelLimit = 115,
            GateLogic = 116,
            ItemManaCost = 117,
            Logoff = 118,
            Active = 119,
            AttackHeight = 120,
            NumAttackFailures = 121,
            AiCpThreshold = 122,
            AiAdvancementStrategy = 123,
            Version = 124,
            [SendOnLogin]
            Age = 125,
            VendorHappyMean = 126,
            VendorHappyVariance = 127,
            CloakStatus = 128,
            [SendOnLogin]
            VitaeCpPool = 129,
            NumServicesSold = 130,
            MaterialType = 131,
            [SendOnLogin]
            NumAllegianceBreaks = 132,
            [Ephemeral]
            ShowableOnRadar = 133,
            [SendOnLogin]
            PlayerKillerStatus = 134,
            VendorHappyMaxItems = 135,
            ScorePageNum = 136,
            ScoreConfigNum = 137,
            ScoreNumScores = 138,
            [SendOnLogin]
            DeathLevel = 139,
            AiOptions = 140,
            OpenToEveryone = 141,
            GeneratorTimeType = 142,
            GeneratorStartTime = 143,
            GeneratorEndTime = 144,
            GeneratorEndDestructionType = 145,
            XpOverride = 146,
            NumCrashAndTurns = 147,
            ComponentWarningThreshold = 148,
            HouseStatus = 149,
            [ServerOnly]
            HookPlacement = 150,
            [ServerOnly]
            HookType = 151,
            [ServerOnly]
            HookItemType = 152,
            AiPpThreshold = 153,
            GeneratorVersion = 154,
            HouseType = 155,
            PickupEmoteOffset = 156,
            WeenieIteration = 157,
            WieldRequirements = 158,
            WieldSkillType = 159,
            WieldDifficulty = 160,
            HouseMaxHooksUsable = 161,
            HouseCurrentHooksUsable = 162,
            AllegianceMinLevel = 163,
            AllegianceMaxLevel = 164,
            HouseRelinkHookCount = 165,
            SlayerCreatureType = 166,
            ConfirmationInProgress = 167,
            ConfirmationTypeInProgress = 168,
            TsysMutationData = 169,
            NumItemsInMaterial = 170,
            NumTimesTinkered = 171,
            AppraisalLongDescDecoration = 172,
            AppraisalLockpickSuccessPercent = 173,
            [Ephemeral]
            AppraisalPages = 174,
            [Ephemeral]
            AppraisalMaxPages = 175,
            AppraisalItemSkill = 176,
            GemCount = 177,
            GemType = 178,
            ImbuedEffect = 179,
            AttackersRawSkillValue = 180,
            [SendOnLogin]
            ChessRank = 181,
            ChessTotalGames = 182,
            ChessGamesWon = 183,
            ChessGamesLost = 184,
            TypeOfAlteration = 185,
            SkillToBeAltered = 186,
            SkillAlterationCount = 187,
            [SendOnLogin]
            HeritageGroup = 188,
            TransferFromAttribute = 189,
            TransferToAttribute = 190,
            AttributeTransferCount = 191,
            [SendOnLogin]
            FakeFishingSkill = 192,
            NumKeys = 193,
            DeathTimestamp = 194,
            PkTimestamp = 195,
            VictimTimestamp = 196,
            HookGroup = 197,
            AllegianceSwearTimestamp = 198,
            [SendOnLogin]
            HousePurchaseTimestamp = 199,
            RedirectableEquippedArmorCount = 200,
            MeleeDefenseImbuedEffectTypeCache = 201,
            MissileDefenseImbuedEffectTypeCache = 202,
            MagicDefenseImbuedEffectTypeCache = 203,
            ElementalDamageBonus = 204,
            ImbueAttempts = 205,
            ImbueSuccesses = 206,
            CreatureKills = 207,
            PlayerKillsPk = 208,
            PlayerKillsPkl = 209,
            RaresTierOne = 210,
            RaresTierTwo = 211,
            RaresTierThree = 212,
            RaresTierFour = 213,
            RaresTierFive = 214,
            [SendOnLogin]
            AugmentationStat = 215,
            [SendOnLogin]
            AugmentationFamilyStat = 216,
            [SendOnLogin]
            AugmentationInnateFamily = 217,
            [SendOnLogin]
            AugmentationInnateStrength = 218,
            [SendOnLogin]
            AugmentationInnateEndurance = 219,
            [SendOnLogin]
            AugmentationInnateCoordination = 220,
            [SendOnLogin]
            AugmentationInnateQuickness = 221,
            [SendOnLogin]
            AugmentationInnateFocus = 222,
            [SendOnLogin]
            AugmentationInnateSelf = 223,
            [SendOnLogin]
            AugmentationSpecializeSalvaging = 224,
            [SendOnLogin]
            AugmentationSpecializeItemTinkering = 225,
            [SendOnLogin]
            AugmentationSpecializeArmorTinkering = 226,
            [SendOnLogin]
            AugmentationSpecializeMagicItemTinkering = 227,
            [SendOnLogin]
            AugmentationSpecializeWeaponTinkering = 228,
            [SendOnLogin]
            AugmentationExtraPackSlot = 229,
            [SendOnLogin]
            AugmentationIncreasedCarryingCapacity = 230,
            [SendOnLogin]
            AugmentationLessDeathItemLoss = 231,
            [SendOnLogin]
            AugmentationSpellsRemainPastDeath = 232,
            [SendOnLogin]
            AugmentationCriticalDefense = 233,
            [SendOnLogin]
            AugmentationBonusXp = 234,
            [SendOnLogin]
            AugmentationBonusSalvage = 235,
            [SendOnLogin]
            AugmentationBonusImbueChance = 236,
            [SendOnLogin]
            AugmentationFasterRegen = 237,
            [SendOnLogin]
            AugmentationIncreasedSpellDuration = 238,
            [SendOnLogin]
            AugmentationResistanceFamily = 239,
            [SendOnLogin]
            AugmentationResistanceSlash = 240,
            [SendOnLogin]
            AugmentationResistancePierce = 241,
            [SendOnLogin]
            AugmentationResistanceBlunt = 242,
            [SendOnLogin]
            AugmentationResistanceAcid = 243,
            [SendOnLogin]
            AugmentationResistanceFire = 244,
            [SendOnLogin]
            AugmentationResistanceFrost = 245,
            [SendOnLogin]
            AugmentationResistanceLightning = 246,
            RaresTierOneLogin = 247,
            RaresTierTwoLogin = 248,
            RaresTierThreeLogin = 249,
            RaresTierFourLogin = 250,
            RaresTierFiveLogin = 251,
            RaresLoginTimestamp = 252,
            RaresTierSix = 253,
            RaresTierSeven = 254,
            RaresTierSixLogin = 255,
            RaresTierSevenLogin = 256,
            ItemAttributeLimit = 257,
            ItemAttributeLevelLimit = 258,
            ItemAttribute2ndLimit = 259,
            ItemAttribute2ndLevelLimit = 260,
            CharacterTitleId = 261,
            NumCharacterTitles = 262,
            ResistanceModifierType = 263,
            FreeTinkersBitfield = 264,
            EquipmentSetId = 265,
            PetClass = 266,
            Lifespan = 267,
            [Ephemeral]
            RemainingLifespan = 268,
            UseCreateQuantity = 269,
            WieldRequirements2 = 270,
            WieldSkillType2 = 271,
            WieldDifficulty2 = 272,
            WieldRequirements3 = 273,
            WieldSkillType3 = 274,
            WieldDifficulty3 = 275,
            WieldRequirements4 = 276,
            WieldSkillType4 = 277,
            WieldDifficulty4 = 278,
            Unique = 279,
            SharedCooldown = 280,
            Faction1Bits = 281,
            Faction2Bits = 282,
            Faction3Bits = 283,
            Hatred1Bits = 284,
            Hatred2Bits = 285,
            Hatred3Bits = 286,
            SocietyRankCelhan = 287,
            SocietyRankEldweb = 288,
            SocietyRankRadblo = 289,
            HearLocalSignals = 290,
            HearLocalSignalsRadius = 291,
            Cleaving = 292,
            [SendOnLogin]
            AugmentationSpecializeGearcraft = 293,
            [SendOnLogin]
            AugmentationInfusedCreatureMagic = 294,
            [SendOnLogin]
            AugmentationInfusedItemMagic = 295,
            [SendOnLogin]
            AugmentationInfusedLifeMagic = 296,
            [SendOnLogin]
            AugmentationInfusedWarMagic = 297,
            [SendOnLogin]
            AugmentationCriticalExpertise = 298,
            [SendOnLogin]
            AugmentationCriticalPower = 299,
            [SendOnLogin]
            AugmentationSkilledMelee = 300,
            [SendOnLogin]
            AugmentationSkilledMissile = 301,
            [SendOnLogin]
            AugmentationSkilledMagic = 302,
            ImbuedEffect2 = 303,
            ImbuedEffect3 = 304,
            ImbuedEffect4 = 305,
            ImbuedEffect5 = 306,
            [SendOnLogin]
            DamageRating = 307,
            [SendOnLogin]
            DamageResistRating = 308,
            [SendOnLogin]
            AugmentationDamageBonus = 309,
            [SendOnLogin]
            AugmentationDamageReduction = 310,
            ImbueStackingBits = 311,
            [SendOnLogin]
            HealOverTime = 312,
            [SendOnLogin]
            CritRating = 313,
            [SendOnLogin]
            CritDamageRating = 314,
            [SendOnLogin]
            CritResistRating = 315,
            [SendOnLogin]
            CritDamageResistRating = 316,
            [SendOnLogin]
            HealingResistRating = 317,
            [SendOnLogin]
            DamageOverTime = 318,
            ItemMaxLevel = 319,
            ItemXpStyle = 320,
            EquipmentSetExtra = 321,
            [SendOnLogin]
            AetheriaBitfield = 322,
            [SendOnLogin]
            HealingBoostRating = 323,
            HeritageSpecificArmor = 324,
            AlternateRacialSkills = 325,
            [SendOnLogin]
            AugmentationJackOfAllTrades = 326,
            [SendOnLogin]
            AugmentationResistanceNether = 327,
            [SendOnLogin]
            AugmentationInfusedVoidMagic = 328,
            [SendOnLogin]
            WeaknessRating = 329,
            [SendOnLogin]
            NetherOverTime = 330,
            [SendOnLogin]
            NetherResistRating = 331,
            LuminanceAward = 332,
            [SendOnLogin]
            LumAugDamageRating = 333,
            [SendOnLogin]
            LumAugDamageReductionRating = 334,
            [SendOnLogin]
            LumAugCritDamageRating = 335,
            [SendOnLogin]
            LumAugCritReductionRating = 336,
            [SendOnLogin]
            LumAugSurgeEffectRating = 337,
            [SendOnLogin]
            LumAugSurgeChanceRating = 338,
            [SendOnLogin]
            LumAugItemManaUsage = 339,
            [SendOnLogin]
            LumAugItemManaGain = 340,
            [SendOnLogin]
            LumAugVitality = 341,
            [SendOnLogin]
            LumAugHealingRating = 342,
            [SendOnLogin]
            LumAugSkilledCraft = 343,
            [SendOnLogin]
            LumAugSkilledSpec = 344,
            [SendOnLogin]
            LumAugNoDestroyCraft = 345,
            RestrictInteraction = 346,
            OlthoiLootTimestamp = 347,
            OlthoiLootStep = 348,
            UseCreatesContractId = 349,
            [SendOnLogin]
            DotResistRating = 350,
            [SendOnLogin]
            LifeResistRating = 351,
            CloakWeaveProc = 352,
            WeaponType = 353,
            [SendOnLogin]
            MeleeMastery = 354,
            [SendOnLogin]
            RangedMastery = 355,
            SneakAttackRating = 356,
            RecklessnessRating = 357,
            DeceptionRating = 358,
            CombatPetRange = 359,
            [SendOnLogin]
            WeaponAuraDamage = 360,
            [SendOnLogin]
            WeaponAuraSpeed = 361,
            [SendOnLogin]
            SummoningMastery = 362,
            HeartbeatLifespan = 363,
            UseLevelRequirement = 364,
            [SendOnLogin]
            LumAugAllSkills = 365,
            UseRequiresSkill = 366,
            UseRequiresSkillLevel = 367,
            UseRequiresSkillSpec = 368,
            UseRequiresLevel = 369,
            [SendOnLogin]
            GearDamage = 370,
            [SendOnLogin]
            GearDamageResist = 371,
            [SendOnLogin]
            GearCrit = 372,
            [SendOnLogin]
            GearCritResist = 373,
            [SendOnLogin]
            GearCritDamage = 374,
            [SendOnLogin]
            GearCritDamageResist = 375,
            [SendOnLogin]
            GearHealingBoost = 376,
            [SendOnLogin]
            GearNetherResist = 377,
            [SendOnLogin]
            GearLifeResist = 378,
            [SendOnLogin]
            GearMaxHealth = 379,
            Unknown380 = 380,
            [SendOnLogin]
            PKDamageRating = 381,
            [SendOnLogin]
            PKDamageResistRating = 382,
            [SendOnLogin]
            GearPKDamageRating = 383,
            [SendOnLogin]
            GearPKDamageResistRating = 384,
            Unknown385 = 385,
            /// <summary>
            /// Overpower chance % for endgame creatures.
            /// </summary>
            [SendOnLogin]
            Overpower = 386,
            [SendOnLogin]
            OverpowerResist = 387,
            // Client does not display accurately
            [SendOnLogin]
            GearOverpower = 388,
            // Client does not display accurately
            [SendOnLogin]
            GearOverpowerResist = 389,
            // Number of times a character has enlightened
            [SendOnLogin]
            Enlightenment = 390,


            // ACE Specific
            [ServerOnly]
            PCAPRecordedAutonomousMovement = 8007,
            [ServerOnly]
            PCAPRecordedMaxVelocityEstimated = 8030,
            [ServerOnly]
            PCAPRecordedPlacement = 8041,
            [ServerOnly]
            PCAPRecordedAppraisalPages = 8042,
            [ServerOnly]
            PCAPRecordedAppraisalMaxPages = 8043,

            //[ServerOnly]
            //TotalLogins                              = 9001,
            //[ServerOnly]
            //DeletionTimestamp                        = 9002,
            //[ServerOnly]
            //CharacterOptions1                        = 9003,
            //[ServerOnly]
            //CharacterOptions2                        = 9004,
            //[ServerOnly]
            //LootTier                                 = 9005,
            //[ServerOnly]
            //GeneratorProbability                     = 9006,
            //[ServerOnly]
            //WeenieType                               = 9007 // I don't think this property type is needed anymore. We don't store the weenie type in the property bags, we store it as a separate field in the base objects.
            [ServerOnly]
            CurrentLoyaltyAtLastLogoff = 9008,
            [ServerOnly]
            CurrentLeadershipAtLastLogoff = 9009,
            [ServerOnly]
            AllegianceOfficerRank = 9010,
            [ServerOnly]
            HouseRentTimestamp = 9011,
            /// <summary>
            ///  Stores the player's selected hairstyle at creation or after a barber use. This is used only for Gear Knights and Olthoi characters who have more than a single part/texture for a "hairstyle" (BodyStyle)
            /// </summary>
            [ServerOnly]
            Hairstyle = 9012,
            /// <summary>
            /// Used to store the calculated Clothing Priority for use with armor reduced items and items like Over-Robes.
            /// </summary>
            [Ephemeral]
            [ServerOnly]
            VisualClothingPriority = 9013,
            [ServerOnly]
            SquelchGlobal = 9014,

            /// <summary>
            /// TODO: This is a place holder for future use. See PlacementPosition
            /// This is the sort order for items in a container
            /// </summary>
            [ServerOnly]
            InventoryOrder = 9015,

            // Decal Specific
            WeenieClassId_Decal = 218103808,
            Icon_Decal_DID = 218103809,
            Container_Decal_IID = 218103810,
            Landblock_Decal = 218103811,
            ItemSlots_Decal = 218103812,
            PackSlots_Decal = 218103813,
            StackCount_Decal = 218103814,
            StackMax_Decal = 218103815,
            Spell_Decal_DID = 218103816,
            SlotLegacy_Decal = 218103817,
            Wielder_Decal_IID = 218103818,
            WieldingSlot_Decal = 218103819,
            Monarch_Decal_IID = 218103820,
            Coverage_Decal = 218103821,
            EquipableSlots_Decal = 218103822,
            EquipType_Decal = 218103823,
            IconOutline_Decal = 218103824,
            MissileType_Decal = 218103825,
            UsageMask_Decal = 218103826,
            HouseOwner_Decal_IID = 218103827,
            HookMask_Decal = 218103828,
            HookType_Decal = 218103829,
            Setup_Decal_DID = 218103830,
            ObjectDescriptionFlags_Decal = 218103831,
            CreateFlags1_Decal = 218103832,
            CreateFlags2_Decal = 218103833,
            Category_Decal = 218103834,
            Behavior_Decal = 218103835,
            MagicDef_Decal = 218103836,
            SpecialProps_Decal = 218103837,
            SpellCount_Decal = 218103838,
            WeapSpeed_Decal = 218103839,
            EquipSkill_Decal = 218103840,
            DamageType_Decal = 218103841,
            MaxDamage_Decal = 218103842,
            Unknown10_Decal = 218103843, // CurrentWieldLocation?
            Unknown100000_Decal = 218103844, // RadarBlipColor ???
            Unknown800000_Decal = 218103845,
            Unknown8000000_Decal = 218103846,
            PhysicsDataFlags_Decal = 218103847,
            ActiveSpellCount_Decal = 218103848,
            IconOverlay_Decal_DID = 218103849,
            IconUnderlay_Decal_DID = 218103850,
            Slot_Decal = 231735296,
        }

        public static class IntValueKeyTools
        {
            /// <summary>
            /// Converts a decal specific IntValueKey to the actual IntValueKey.
            /// If this is not an IntValueKey, 0 will be returned.
            /// </summary>
            public static uint ConvertToInt(IntValueKey input)
            {
                if (input == IntValueKey.Category_Decal) return (int)IntValueKey.ItemType;
                if (input == IntValueKey.Coverage_Decal) return (int)IntValueKey.ClothingPriority;
                if (input == IntValueKey.ItemSlots_Decal) return (int)IntValueKey.ItemsCapacity;
                if (input == IntValueKey.PackSlots_Decal) return (int)IntValueKey.ContainersCapacity;
                if (input == IntValueKey.EquipableSlots_Decal) return (int)IntValueKey.ValidLocations;
                //if (input == IntValueKey.WieldingSlot_Decal)	return (int)IntValueKey.CurrentWieldedLocation;
                if (input == IntValueKey.StackMax_Decal) return (int)IntValueKey.MaxStackSize;
                if (input == IntValueKey.StackCount_Decal) return (int)IntValueKey.StackSize;
                if (input == IntValueKey.IconOutline_Decal) return (int)IntValueKey.UiEffects;
                if (input == IntValueKey.MaxDamage_Decal) return (int)IntValueKey.Damage;
                if (input == IntValueKey.DamageType_Decal) return (int)IntValueKey.DamageType;
                if (input == IntValueKey.EquipSkill_Decal) return (int)IntValueKey.WeaponSkill;
                if (input == IntValueKey.WeapSpeed_Decal) return (int)IntValueKey.WeaponTime;
                if (input == IntValueKey.MissileType_Decal) return (int)IntValueKey.AmmoType;
                if (input == IntValueKey.EquipType_Decal) return (int)IntValueKey.CombatUse;
                if (input == IntValueKey.UsageMask_Decal) return (int)IntValueKey.TargetType;
                if (input == IntValueKey.HookMask_Decal) return (int)IntValueKey.HookType;

                return 0;
            }

            /// <summary>
            /// If input is not a IID, 0 will be returned
            /// </summary>
            public static uint ConvertToIID(IntValueKey input)
            {
                if (input == IntValueKey.Container_Decal_IID) return 2;  // CONTAINER_IID
                if (input == IntValueKey.Wielder_Decal_IID) return 3;  // WIELDER_IID
                if (input == IntValueKey.Monarch_Decal_IID) return 26; // MONARCH_IID
                if (input == IntValueKey.HouseOwner_Decal_IID) return 32; // HOUSE_OWNER_IID

                return 0;
            }

            /// <summary>
            /// If input is not a DID, 0 will be returned
            /// </summary>
            public static uint ConvertToDID(IntValueKey input)
            {
                if (input == IntValueKey.Setup_Decal_DID) return 1;  // SETUP_DID
                if (input == IntValueKey.Icon_Decal_DID) return 8;  // ICON_DID
                if (input == IntValueKey.Spell_Decal_DID) return 28; // SPELL_DID
                if (input == IntValueKey.IconOverlay_Decal_DID) return 50; // ICON_OVERLAY_DID
                if (input == IntValueKey.IconUnderlay_Decal_DID) return 52; // ICON_UNDERLAY_DID

                return 0;
            }
        }

        // https://github.com/ACEmulator/ACE/blob/master/Source/ACE.Entity/Enum/Properties/PropertyBool.cs
        public enum BoolValueKey
        {
            // properties marked as ServerOnly are properties we never saw in PCAPs, from here:
            // http://ac.yotesfan.com/ace_object/not_used_enums.php
            // source: @OptimShi
            // description attributes are used by the weenie editor for a cleaner display name

            Undef = 0,
            [Ephemeral]
            [ServerOnly]
            Stuck = 1,
            [Ephemeral]
            Open = 2,
            Locked = 3,
            RotProof = 4,
            AllegianceUpdateRequest = 5,
            AiUsesMana = 6,
            AiUseHumanMagicAnimations = 7,
            AllowGive = 8,
            CurrentlyAttacking = 9,
            AttackerAi = 10,
            [ServerOnly]
            IgnoreCollisions = 11,
            [ServerOnly]
            ReportCollisions = 12,
            [ServerOnly]
            Ethereal = 13,
            [ServerOnly]
            GravityStatus = 14,
            [ServerOnly]
            LightsStatus = 15,
            [ServerOnly]
            ScriptedCollision = 16,
            [ServerOnly]
            Inelastic = 17,
            [ServerOnly]
            [Ephemeral]
            Visibility = 18,
            [ServerOnly]
            Attackable = 19,
            SafeSpellComponents = 20,
            AdvocateState = 21,
            Inscribable = 22,
            DestroyOnSell = 23,
            UiHidden = 24,
            IgnoreHouseBarriers = 25,
            HiddenAdmin = 26,
            PkWounder = 27,
            PkKiller = 28,
            NoCorpse = 29,
            UnderLifestoneProtection = 30,
            ItemManaUpdatePending = 31,
            [Ephemeral]
            GeneratorStatus = 32,
            [Ephemeral]
            ResetMessagePending = 33,
            DefaultOpen = 34,
            DefaultLocked = 35,
            DefaultOn = 36,
            OpenForBusiness = 37,
            IsFrozen = 38,
            DealMagicalItems = 39,
            LogoffImDead = 40,
            ReportCollisionsAsEnvironment = 41,
            AllowEdgeSlide = 42,
            AdvocateQuest = 43,
            [Ephemeral]
            [SendOnLogin]
            IsAdmin = 44,
            [Ephemeral]
            [SendOnLogin]
            IsArch = 45,
            [Ephemeral]
            [SendOnLogin]
            IsSentinel = 46,
            [SendOnLogin]
            IsAdvocate = 47,
            CurrentlyPoweringUp = 48,
            [Ephemeral]
            GeneratorEnteredWorld = 49,
            NeverFailCasting = 50,
            VendorService = 51,
            AiImmobile = 52,
            DamagedByCollisions = 53,
            IsDynamic = 54,
            IsHot = 55,
            IsAffecting = 56,
            AffectsAis = 57,
            SpellQueueActive = 58,
            [Ephemeral]
            GeneratorDisabled = 59,
            IsAcceptingTells = 60,
            LoggingChannel = 61,
            OpensAnyLock = 62,
            UnlimitedUse = 63,
            GeneratedTreasureItem = 64,
            IgnoreMagicResist = 65,
            IgnoreMagicArmor = 66,
            AiAllowTrade = 67,
            [SendOnLogin]
            SpellComponentsRequired = 68,
            IsSellable = 69,
            IgnoreShieldsBySkill = 70,
            NoDraw = 71,
            ActivationUntargeted = 72,
            HouseHasGottenPriorityBootPos = 73,
            [Ephemeral]
            GeneratorAutomaticDestruction = 74,
            HouseHooksVisible = 75,
            HouseRequiresMonarch = 76,
            HouseHooksEnabled = 77,
            HouseNotifiedHudOfHookCount = 78,
            AiAcceptEverything = 79,
            IgnorePortalRestrictions = 80,
            RequiresBackpackSlot = 81,
            DontTurnOrMoveWhenGiving = 82,
            [ServerOnly]
            NpcLooksLikeObject = 83,
            IgnoreCloIcons = 84,
            AppraisalHasAllowedWielder = 85,
            ChestRegenOnClose = 86,
            LogoffInMinigame = 87,
            PortalShowDestination = 88,
            PortalIgnoresPkAttackTimer = 89,
            NpcInteractsSilently = 90,
            Retained = 91,
            IgnoreAuthor = 92,
            Limbo = 93,
            AppraisalHasAllowedActivator = 94,
            ExistedBeforeAllegianceXpChanges = 95,
            IsDeaf = 96,
            [Ephemeral]
            [SendOnLogin]
            IsPsr = 97,
            Invincible = 98,
            Ivoryable = 99,
            Dyable = 100,
            CanGenerateRare = 101,
            CorpseGeneratedRare = 102,
            NonProjectileMagicImmune = 103,
            [SendOnLogin]
            ActdReceivedItems = 104,
            Unknown105 = 105,
            [Ephemeral]
            FirstEnterWorldDone = 106,
            RecallsDisabled = 107,
            RareUsesTimer = 108,
            ActdPreorderReceivedItems = 109,
            Afk = 110,
            IsGagged = 111,
            ProcSpellSelfTargeted = 112,
            IsAllegianceGagged = 113,
            EquipmentSetTriggerPiece = 114,
            Uninscribe = 115,
            WieldOnUse = 116,
            ChestClearedWhenClosed = 117,
            NeverAttack = 118,
            SuppressGenerateEffect = 119,
            TreasureCorpse = 120,
            EquipmentSetAddLevel = 121,
            BarberActive = 122,
            TopLayerPriority = 123,
            NoHeldItemShown = 124,
            LoginAtLifestone = 125,
            OlthoiPk = 126,
            [SendOnLogin]
            Account15Days = 127,
            HadNoVitae = 128,
            NoOlthoiTalk = 129,
            AutowieldLeft = 130,


            // ACE Specific
            /* custom */
            [ServerOnly]
            LinkedPortalOneSummon = 9001,
            [ServerOnly]
            LinkedPortalTwoSummon = 9002,
            [ServerOnly]
            HouseEvicted = 9003,
            [ServerOnly]
            UntrainedSkills = 9004,


            // Decal Specific
            Lockable_Decal = 201326592,
            Inscribable_Decal = 201326593,
        }

        public static class BoolValueKeyTools
        {
            /// <summary>
            /// Converts a decal specific IntValueKey to the actual IntValueKey.
            /// If this is not an IntValueKey, 0 will be returned.
            /// </summary>
            public static uint ConvertToString(BoolValueKey input)
            {
                if (input == BoolValueKey.Lockable_Decal) return (int)BoolValueKey.Locked;
                if (input == BoolValueKey.Inscribable_Decal) return (int)BoolValueKey.Inscribable;

                return 0;
            }
        }

        // https://github.com/ACEmulator/ACE/blob/master/Source/ACE.Entity/Enum/Properties/PropertyFloat.cs
        public enum DoubleValueKey
        {
            // properties marked as ServerOnly are properties we never saw in PCAPs, from here:
            // http://ac.yotesfan.com/ace_object/not_used_enums.php
            // source: @OptimShi
            // description attributes are used by the weenie editor for a cleaner display name

            Undef = 0,
            HeartbeatInterval = 1,
            [Ephemeral]
            HeartbeatTimestamp = 2,
            HealthRate = 3,
            StaminaRate = 4,
            ManaRate = 5,
            HealthUponResurrection = 6,
            StaminaUponResurrection = 7,
            ManaUponResurrection = 8,
            StartTime = 9,
            StopTime = 10,
            ResetInterval = 11,
            Shade = 12,
            ArmorModVsSlash = 13,
            ArmorModVsPierce = 14,
            ArmorModVsBludgeon = 15,
            ArmorModVsCold = 16,
            ArmorModVsFire = 17,
            ArmorModVsAcid = 18,
            ArmorModVsElectric = 19,
            CombatSpeed = 20,
            WeaponLength = 21,
            DamageVariance = 22,
            CurrentPowerMod = 23,
            AccuracyMod = 24,
            StrengthMod = 25,
            MaximumVelocity = 26,
            RotationSpeed = 27,
            MotionTimestamp = 28,
            WeaponDefense = 29,
            WimpyLevel = 30,
            VisualAwarenessRange = 31,
            AuralAwarenessRange = 32,
            PerceptionLevel = 33,
            PowerupTime = 34,
            MaxChargeDistance = 35,
            ChargeSpeed = 36,
            BuyPrice = 37,
            SellPrice = 38,
            DefaultScale = 39,
            LockpickMod = 40,
            RegenerationInterval = 41,
            RegenerationTimestamp = 42,
            GeneratorRadius = 43,
            TimeToRot = 44,
            DeathTimestamp = 45,
            PkTimestamp = 46,
            VictimTimestamp = 47,
            LoginTimestamp = 48,
            CreationTimestamp = 49,
            MinimumTimeSincePk = 50,
            DeprecatedHousekeepingPriority = 51,
            AbuseLoggingTimestamp = 52,
            LastPortalTeleportTimestamp = 53,
            UseRadius = 54,
            HomeRadius = 55,
            ReleasedTimestamp = 56,
            MinHomeRadius = 57,
            Facing = 58,
            ResetTimestamp = 59,
            LogoffTimestamp = 60,
            EconRecoveryInterval = 61,
            WeaponOffense = 62,
            DamageMod = 63,
            ResistSlash = 64,
            ResistPierce = 65,
            ResistBludgeon = 66,
            ResistFire = 67,
            ResistCold = 68,
            ResistAcid = 69,
            ResistElectric = 70,
            ResistHealthBoost = 71,
            ResistStaminaDrain = 72,
            ResistStaminaBoost = 73,
            ResistManaDrain = 74,
            ResistManaBoost = 75,
            [Ephemeral]
            Translucency = 76,
            PhysicsScriptIntensity = 77,
            Friction = 78,
            Elasticity = 79,
            AiUseMagicDelay = 80,
            ItemMinSpellcraftMod = 81,
            ItemMaxSpellcraftMod = 82,
            ItemRankProbability = 83,
            Shade2 = 84,
            Shade3 = 85,
            Shade4 = 86,
            ItemEfficiency = 87,
            ItemManaUpdateTimestamp = 88,
            SpellGestureSpeedMod = 89,
            SpellStanceSpeedMod = 90,
            AllegianceAppraisalTimestamp = 91,
            PowerLevel = 92,
            AccuracyLevel = 93,
            AttackAngle = 94,
            AttackTimestamp = 95,
            CheckpointTimestamp = 96,
            SoldTimestamp = 97,
            UseTimestamp = 98,
            UseLockTimestamp = 99,
            HealkitMod = 100,
            FrozenTimestamp = 101,
            HealthRateMod = 102,
            AllegianceSwearTimestamp = 103,
            ObviousRadarRange = 104,
            HotspotCycleTime = 105,
            HotspotCycleTimeVariance = 106,
            SpamTimestamp = 107,
            SpamRate = 108,
            BondWieldedTreasure = 109,
            BulkMod = 110,
            SizeMod = 111,
            GagTimestamp = 112,
            GeneratorUpdateTimestamp = 113,
            DeathSpamTimestamp = 114,
            DeathSpamRate = 115,
            WildAttackProbability = 116,
            FocusedProbability = 117,
            CrashAndTurnProbability = 118,
            CrashAndTurnRadius = 119,
            CrashAndTurnBias = 120,
            GeneratorInitialDelay = 121,
            AiAcquireHealth = 122,
            AiAcquireStamina = 123,
            AiAcquireMana = 124,
            /// <summary>
            /// this had a default of "1" - leaving comment to investigate potential options for defaulting these things (125)
            /// </summary>
            [SendOnLogin]
            ResistHealthDrain = 125,
            LifestoneProtectionTimestamp = 126,
            AiCounteractEnchantment = 127,
            AiDispelEnchantment = 128,
            TradeTimestamp = 129,
            AiTargetedDetectionRadius = 130,
            EmotePriority = 131,
            [Ephemeral]
            LastTeleportStartTimestamp = 132,
            EventSpamTimestamp = 133,
            EventSpamRate = 134,
            InventoryOffset = 135,
            CriticalMultiplier = 136,
            ManaStoneDestroyChance = 137,
            SlayerDamageBonus = 138,
            AllegianceInfoSpamTimestamp = 139,
            AllegianceInfoSpamRate = 140,
            NextSpellcastTimestamp = 141,
            [Ephemeral]
            AppraisalRequestedTimestamp = 142,
            AppraisalHeartbeatDueTimestamp = 143,
            ManaConversionMod = 144,
            LastPkAttackTimestamp = 145,
            FellowshipUpdateTimestamp = 146,
            CriticalFrequency = 147,
            LimboStartTimestamp = 148,
            WeaponMissileDefense = 149,
            WeaponMagicDefense = 150,
            IgnoreShield = 151,
            ElementalDamageMod = 152,
            StartMissileAttackTimestamp = 153,
            LastRareUsedTimestamp = 154,
            IgnoreArmor = 155,
            ProcSpellRate = 156,
            ResistanceModifier = 157,
            AllegianceGagTimestamp = 158,
            AbsorbMagicDamage = 159,
            CachedMaxAbsorbMagicDamage = 160,
            GagDuration = 161,
            AllegianceGagDuration = 162,
            [SendOnLogin]
            GlobalXpMod = 163,
            HealingModifier = 164,
            ArmorModVsNether = 165,
            ResistNether = 166,
            CooldownDuration = 167,
            [SendOnLogin]
            WeaponAuraOffense = 168,
            [SendOnLogin]
            WeaponAuraDefense = 169,
            [SendOnLogin]
            WeaponAuraElemental = 170,
            [SendOnLogin]
            WeaponAuraManaConv = 171,


            // ACE Specific
            [ServerOnly]
            PCAPRecordedWorkmanship = 8004,
            [ServerOnly]
            PCAPRecordedVelocityX = 8010,
            [ServerOnly]
            PCAPRecordedVelocityY = 8011,
            [ServerOnly]
            PCAPRecordedVelocityZ = 8012,
            [ServerOnly]
            PCAPRecordedAccelerationX = 8013,
            [ServerOnly]
            PCAPRecordedAccelerationY = 8014,
            [ServerOnly]
            PCAPRecordedAccelerationZ = 8015,
            [ServerOnly]
            PCAPRecordeOmegaX = 8016,
            [ServerOnly]
            PCAPRecordeOmegaY = 8017,
            [ServerOnly]
            PCAPRecordeOmegaZ = 8018,


            // Decal Specific
            SlashProt_Decal = 167772160,
            PierceProt_Decal = 167772161,
            BludgeonProt_Decal = 167772162,
            AcidProt_Decal = 167772163,
            LightningProt_Decal = 167772164,
            FireProt_Decal = 167772165,
            ColdProt_Decal = 167772166,
            Heading_Decal = 167772167,
            ApproachDistance_Decal = 167772168,
            SalvageWorkmanship_Decal = 167772169,
            Scale_Decal = 167772170,
            Variance_Decal = 167772171,
            AttackBonus_Decal = 167772172,
            Range_Decal = 167772173,
            DamageBonus_Decal = 167772174,
        }

        public static class DoubleValueKeyTools
        {
            /// <summary>
            /// Converts a decal specific IntValueKey to the actual IntValueKey.
            /// If this is not an IntValueKey, 0 will be returned.
            /// </summary>
            public static uint ConvertToDouble(DoubleValueKey input)
            {
                if (input == DoubleValueKey.SlashProt_Decal) return (int)DoubleValueKey.ArmorModVsSlash;
                if (input == DoubleValueKey.PierceProt_Decal) return (int)DoubleValueKey.ArmorModVsPierce;
                if (input == DoubleValueKey.BludgeonProt_Decal) return (int)DoubleValueKey.ArmorModVsBludgeon;
                if (input == DoubleValueKey.AcidProt_Decal) return (int)DoubleValueKey.ArmorModVsAcid;
                if (input == DoubleValueKey.LightningProt_Decal) return (int)DoubleValueKey.ArmorModVsElectric;
                if (input == DoubleValueKey.FireProt_Decal) return (int)DoubleValueKey.ArmorModVsFire;
                if (input == DoubleValueKey.ColdProt_Decal) return (int)DoubleValueKey.ArmorModVsCold;

                if (input == DoubleValueKey.ApproachDistance_Decal) return (int)DoubleValueKey.UseRadius;
                if (input == DoubleValueKey.Scale_Decal) return (int)DoubleValueKey.DefaultScale;
                if (input == DoubleValueKey.Variance_Decal) return (int)DoubleValueKey.DamageVariance;
                if (input == DoubleValueKey.AttackBonus_Decal) return (int)DoubleValueKey.WeaponOffense; ;
                if (input == DoubleValueKey.Range_Decal) return (int)DoubleValueKey.MaximumVelocity;
                if (input == DoubleValueKey.DamageBonus_Decal) return (int)DoubleValueKey.DamageMod;

                return 0;
            }
        }

        // https://github.com/ACEmulator/ACE/blob/master/Source/ACE.Entity/Enum/Properties/PropertyString.cs
        public enum StringValueKey
        {
            // properties marked as ServerOnly are properties we never saw in PCAPs, from here:
            // http://ac.yotesfan.com/ace_object/not_used_enums.php
            // source: @OptimShi
            // description attributes are used by the weenie editor for a cleaner display name
            Undef = 0,
            [SendOnLogin]
            Name = 1,
            /// <summary>
            /// default "Adventurer"
            /// </summary>
            Title = 2,
            Sex = 3,
            HeritageGroup = 4,
            Template = 5,
            AttackersName = 6,
            Inscription = 7,
            [Description("Scribe Name")]
            ScribeName = 8,
            VendorsName = 9,
            Fellowship = 10,
            MonarchsName = 11,
            [ServerOnly]
            LockCode = 12,
            [ServerOnly]
            KeyCode = 13,
            Use = 14,
            ShortDesc = 15,
            LongDesc = 16,
            ActivationTalk = 17,
            [ServerOnly]
            UseMessage = 18,
            ItemHeritageGroupRestriction = 19,
            PluralName = 20,
            MonarchsTitle = 21,
            ActivationFailure = 22,
            ScribeAccount = 23,
            TownName = 24,
            CraftsmanName = 25,
            UsePkServerError = 26,
            ScoreCachedText = 27,
            ScoreDefaultEntryFormat = 28,
            ScoreFirstEntryFormat = 29,
            ScoreLastEntryFormat = 30,
            ScoreOnlyEntryFormat = 31,
            ScoreNoEntry = 32,
            [ServerOnly]
            Quest = 33,
            GeneratorEvent = 34,
            PatronsTitle = 35,
            HouseOwnerName = 36,
            QuestRestriction = 37,
            AppraisalPortalDestination = 38,
            TinkerName = 39,
            ImbuerName = 40,
            HouseOwnerAccount = 41,
            DisplayName = 42,
            DateOfBirth = 43,
            ThirdPartyApi = 44,
            KillQuest = 45,
            Afk = 46,
            AllegianceName = 47,
            AugmentationAddQuest = 48,
            KillQuest2 = 49,
            KillQuest3 = 50,
            UseSendsSignal = 51,

            [Description("Gear Plating Name")]
            GearPlatingName = 52,


            // ACE Specific
            [ServerOnly]
            PCAPRecordedCurrentMotionState = 8006,
            [ServerOnly]
            PCAPRecordedServerName = 8031,
            [ServerOnly]
            PCAPRecordedCharacterName = 8032,

            /* custom */
            [ServerOnly]
            AllegianceMotd = 9001,
            [ServerOnly]
            AllegianceMotdSetBy = 9002,
            [ServerOnly]
            AllegianceSpeakerTitle = 9003,
            [ServerOnly]
            AllegianceSeneschalTitle = 9004,
            [ServerOnly]
            AllegianceCastellanTitle = 9005,
            [ServerOnly]
            GodState = 9006,
            [ServerOnly]
            TinkerLog = 9007,


            // Decal Specific
            SecondaryName_Decal = 184549376,
        }

        public static class StringValueKeyTools
        {
            /// <summary>
            /// Converts a decal specific IntValueKey to the actual IntValueKey.
            /// If this is not an IntValueKey, 0 will be returned.
            /// </summary>
            public static uint ConvertToString(StringValueKey input)
            {
                if (input == StringValueKey.SecondaryName_Decal) return (int)StringValueKey.PluralName;

                return 0;
            }
        }
    }
}
