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
            //case PacketOpcode.Evt_DDD__InterrogationResponse_ID:
            //    {
            //        InterrogationResponse message = InterrogationResponse.read(messageDataReader);
            //        message.contributeToTreeView(outputTreeView);
            //        break;
            //    }
            //case PacketOpcode.Evt_DDD__BeginDDD_ID:
            //    {

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
            for (int i = 0; i < supportedLanguages.Count; i++)
            {
                TreeNode languageNode = setNode.Nodes.Add($"language {i + 1} = ");
                //ContextInfo.AddToList(new ContextInfo { Length = set_[i].Length }, updateDataIndex: false);
                supportedLanguages[i].contributeToTreeNode(languageNode);
            }
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
            return newObj;
        }

        public void contributeToTreeNode(TreeNode node)
        {
            node.Nodes.Add("langugage = " + langugage);
            //ContextInfo.AddToList(new ContextInfo { DataType = DataType.ObjectID });
        }
    }

}
