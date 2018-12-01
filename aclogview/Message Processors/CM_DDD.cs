using aclogview;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

public class CM_DDD : MessageProcessor {

    public override bool acceptMessageData(BinaryReader messageDataReader, TreeView outputTreeView) {
        bool handled = true;

        PacketOpcode opcode = Util.readOpcode(messageDataReader);
        switch (opcode) {
            case PacketOpcode.Evt_DDD__Interrogation_ID:
                {
                    Interrogation message = Interrogation.read(messageDataReader);
                    message.contributeToTreeView(outputTreeView);
                    break;
                }
            case PacketOpcode.Evt_DDD__InterrogationResponse_ID:
                {
                    InterrogationResponse message = InterrogationResponse.read(messageDataReader);
                    message.contributeToTreeView(outputTreeView);
                    break;
                }
            case PacketOpcode.Evt_DDD__BeginDDD_ID:
                {
                    //Join message = Join.read(messageDataReader);
                    //message.contributeToTreeView(outputTreeView);
                    break;
                }
            //case PacketOpcode.Evt_Game__Join_ID:
            //    {
            //        Join message = Join.read(messageDataReader);
            //        message.contributeToTreeView(outputTreeView);
            //        break;
            //    }
            //case PacketOpcode.Evt_Game__Quit_ID:
            //    {
            //        EmptyMessage message = new EmptyMessage(opcode);
            //        message.contributeToTreeView(outputTreeView);
            //        break;
            //    }
            //case PacketOpcode.Evt_Game__Stalemate_ID:
            //    {
            //        Stalemate message = Stalemate.read(messageDataReader);
            //        message.contributeToTreeView(outputTreeView);
            //        break;
            //    }
            //case PacketOpcode.Evt_Game__Recv_JoinGameResponse_ID:
            //    {
            //        Recv_JoinGameResponse message = Recv_JoinGameResponse.read(messageDataReader);
            //        message.contributeToTreeView(outputTreeView);
            //        break;
            //    }
            //case PacketOpcode.Evt_Game__Recv_GameOver_ID:
            //    {
            //        Recv_GameOver message = Recv_GameOver.read(messageDataReader);
            //        message.contributeToTreeView(outputTreeView);
            //        break;
            //    }
            default: {
                    handled = false;
                    break;
                }
        }

        return handled;
    }


    public class Interrogation : Message
    {
        public uint serversRegion;
        public uint nameRuleLanguage;
        public uint productID;
        public uint supportedLanguagesCount;
        public List<Language> supportedLanguages = new List<Language>();

        public static Interrogation read(BinaryReader binaryReader)
        {
            Interrogation newObj = new Interrogation();
            newObj.serversRegion = binaryReader.ReadUInt32();
            newObj.nameRuleLanguage = binaryReader.ReadUInt32();
            newObj.productID = binaryReader.ReadUInt32();
            newObj.supportedLanguagesCount = binaryReader.ReadUInt32();
            for (uint i = 0; i < newObj.supportedLanguagesCount; ++i)
            {
                newObj.supportedLanguages.Add(Language.read(binaryReader));
            }
            return newObj;
        }

        public override void contributeToTreeView(TreeView treeView)
        {
            TreeNode rootNode = new TreeNode(this.GetType().Name);
            rootNode.Expand();
            rootNode.Nodes.Add("serversRegion = " + serversRegion);
            rootNode.Nodes.Add("nameRuleLanguage = " + nameRuleLanguage);
            rootNode.Nodes.Add("productID = " + productID);
            rootNode.Nodes.Add("supportedLanguagesCount = " + supportedLanguagesCount);
            TreeNode setNode = rootNode.Nodes.Add("supportedLanguages = ");
            // Calculate character set size
            //var charSetSize = 4;
            //for (int i = 0; i < set_.Count; i++)
            //{
            //    charSetSize += set_[i].Length;
            //}
            //ContextInfo.AddToList(new ContextInfo { Length = charSetSize }, updateDataIndex: false);
            // Skip character list count uint
            //ContextInfo.DataIndex += 4;
            for (int i = 0; i < supportedLanguages.Count; i++)
            {
                TreeNode languageNode = setNode.Nodes.Add($"language {i + 1} = ");
                //ContextInfo.AddToList(new ContextInfo { Length = set_[i].Length }, updateDataIndex: false);
                supportedLanguages[i].contributeToTreeNode(languageNode);
            }
            //rootNode.Nodes.Add("i_idGame = " + Utility.FormatHex(i_idGame));
            //rootNode.Nodes.Add("i_iWhichTeam = " + i_iWhichTeam);
            treeView.Nodes.Add(rootNode);
        }
    }

    public class Language
    {
        public uint langugage;

        public static Language read(BinaryReader binaryReader)
        {
            Language newObj = new Language();
            var startPosition = binaryReader.BaseStream.Position;
            newObj.langugage = binaryReader.ReadUInt32();
            //newObj.name_ = PStringChar.read(binaryReader);
            //newObj.secondsGreyedOut_ = binaryReader.ReadUInt32();
            //newObj.Length = (int)(binaryReader.BaseStream.Position - startPosition);
            return newObj;
        }

        public void contributeToTreeNode(TreeNode node)
        {
            //node.Nodes.Add("langugage = " + Utility.FormatHex(langugage));
            node.Nodes.Add("langugage = " + langugage);
            //ContextInfo.AddToList(new ContextInfo { DataType = DataType.ObjectID });
            //node.Nodes.Add("name_ = " + name_.m_buffer);
            //ContextInfo.AddToList(new ContextInfo { Length = name_.Length, DataType = DataType.Serialized_AsciiString });
            //node.Nodes.Add("secondsGreyedOut_ = " + secondsGreyedOut_);
            //ContextInfo.AddToList(new ContextInfo { Length = 4 });
        }
    }

    public class InterrogationResponse : Message
    {
        public uint clientLanguage;
        public uint count;
        public List<ulong> files = new List<ulong>();
        public uint count2;
        public List<ulong> files2 = new List<ulong>();
        public uint flags;

        public static InterrogationResponse read(BinaryReader binaryReader)
        {
            InterrogationResponse newObj = new InterrogationResponse();
            newObj.clientLanguage = binaryReader.ReadUInt32();
            newObj.count = binaryReader.ReadUInt32();
            for (uint i = 0; i < newObj.count; ++i)
            {
                newObj.files.Add(binaryReader.ReadUInt64());
            }
            //newObj.count2 = binaryReader.ReadUInt32();
            //for (uint i = 0; i < newObj.count2; ++i)
            //{
            //    newObj.files2.Add(binaryReader.ReadUInt64());
            //}
            //newObj.flags = binaryReader.ReadUInt32();
            return newObj;
        }

        public override void contributeToTreeView(TreeView treeView)
        {
            TreeNode rootNode = new TreeNode(this.GetType().Name);
            rootNode.Expand();
            rootNode.Nodes.Add("clientLanguage = " + clientLanguage);
            rootNode.Nodes.Add("count = " + count);
            TreeNode setNode = rootNode.Nodes.Add("files = ");
            // Calculate character set size
            //var charSetSize = 4;
            //for (int i = 0; i < set_.Count; i++)
            //{
            //    charSetSize += set_[i].Length;
            //}
            //ContextInfo.AddToList(new ContextInfo { Length = charSetSize }, updateDataIndex: false);
            // Skip character list count uint
            //ContextInfo.DataIndex += 4;
            for (int i = 0; i < files.Count; i++)
            {
                TreeNode fileNode = setNode.Nodes.Add($"file {i + 1} = ");
                //ContextInfo.AddToList(new ContextInfo { Length = set_[i].Length }, updateDataIndex: false);
                //supportedLanguages[i].contributeToTreeNode(languageNode);
                fileNode.Nodes.Add("idDataFile = " + files[i]);
            }
            //rootNode.Nodes.Add("i_idGame = " + Utility.FormatHex(i_idGame));
            //rootNode.Nodes.Add("i_iWhichTeam = " + i_iWhichTeam);
            rootNode.Nodes.Add("count2 = " + count2);
            TreeNode set2Node = rootNode.Nodes.Add("files2 = ");
            for (int i = 0; i < files2.Count; i++)
            {
                TreeNode fileNode = set2Node.Nodes.Add($"file {i + 1} = ");
                fileNode.Nodes.Add("idDataFile = " + files2[i]);
            }
            rootNode.Nodes.Add("flags = " + flags);
            treeView.Nodes.Add(rootNode);
        }
    }



    public class Join : Message
    {
        public uint i_idGame; // Gameboard ID
        public int i_iWhichTeam;

        public static Join read(BinaryReader binaryReader)
        {
            Join newObj = new Join();
            newObj.i_idGame = binaryReader.ReadUInt32();
            newObj.i_iWhichTeam = binaryReader.ReadInt32();
            return newObj;
        }

        public override void contributeToTreeView(TreeView treeView)
        {
            TreeNode rootNode = new TreeNode(this.GetType().Name);
            rootNode.Expand();
            rootNode.Nodes.Add("i_idGame = " + Utility.FormatHex(i_idGame)); 
            rootNode.Nodes.Add("i_iWhichTeam = " + i_iWhichTeam);
            treeView.Nodes.Add(rootNode);
        }
    }

    //public class Stalemate : Message
    //{
    //    public int i_fOn;

    //    public static Stalemate read(BinaryReader binaryReader)
    //    {
    //        Stalemate newObj = new Stalemate();
    //        newObj.i_fOn = binaryReader.ReadInt32();
    //        return newObj;
    //    }

    //    public override void contributeToTreeView(TreeView treeView)
    //    {
    //        TreeNode rootNode = new TreeNode(this.GetType().Name);
    //        rootNode.Expand();
    //        rootNode.Nodes.Add("i_fOn = " + i_fOn);
    //        treeView.Nodes.Add(rootNode);
    //    }
    //}


    //public class Recv_JoinGameResponse : Message
    //{
    //    public uint i_idGame;
    //    public int i_iWhichTeam;

    //    public static Recv_JoinGameResponse read(BinaryReader binaryReader)
    //    {
    //        Recv_JoinGameResponse newObj = new Recv_JoinGameResponse();
    //        newObj.i_idGame = binaryReader.ReadUInt32();
    //        newObj.i_iWhichTeam = binaryReader.ReadInt32();
    //        return newObj;
    //    }

    //    public override void contributeToTreeView(TreeView treeView)
    //    {
    //        TreeNode rootNode = new TreeNode(this.GetType().Name);
    //        rootNode.Expand();
    //        rootNode.Nodes.Add("i_idGame = " + Utility.FormatHex(i_idGame));
    //        rootNode.Nodes.Add("i_iWhichTeam = " + i_iWhichTeam);  // TODO: White = 0 (Drudges), Black = 1 (Mosswarts)
    //        treeView.Nodes.Add(rootNode);
    //    }
    //}

    //public class Recv_GameOver : Message
    //{
    //    public uint i_idGame;
    //    public int i_iTeamWinner;

    //    public static Recv_GameOver read(BinaryReader binaryReader)
    //    {
    //        Recv_GameOver newObj = new Recv_GameOver();
    //        newObj.i_idGame = binaryReader.ReadUInt32();
    //        newObj.i_iTeamWinner = binaryReader.ReadInt32();
    //        return newObj;
    //    }

    //    public override void contributeToTreeView(TreeView treeView)
    //    {
    //        TreeNode rootNode = new TreeNode(this.GetType().Name);
    //        rootNode.Expand();
    //        rootNode.Nodes.Add("i_idGame = " + Utility.FormatHex(i_idGame));
    //        rootNode.Nodes.Add("i_iTeamWinner = " + i_iTeamWinner);
    //        treeView.Nodes.Add(rootNode);
    //    }
    //}
}
