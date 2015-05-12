using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.IO;
using BrokerLib;

namespace IntegrationComponent
{
    public interface IIntegrationComponent
    {
        String Name { get; set; }
        System.Object Broker { get; set; }
        void Configurate(string Config);
        void ProcessMessage(string SenderName, string MessageText, System.Object OleVariant);
    }

    public class Logger
    {
        private StreamWriter _log;

        public Logger(string path)
        {
            _log = new StreamWriter(path);
        }

        ~Logger()
        {
            _log.Close();
            _log.Dispose();
        }

        public void log(string text)
        {
            _log.WriteLine(text);
            _log.Flush();
        }
    }

    public class IntegrationComponent : IIntegrationComponent
    {
        //public static System.Object m_broker;
        public static string m_name;
        public static IBroker m_broker;
        private static Logger logger;

        public string Name
        {
            get
            {
                return m_name;
            }
            set
            {
                m_name = value;
            }
        }

        public System.Object Broker
        {
            get
            {
                return m_broker;
            }
            set
            {
                m_broker = (IBroker)value;
            }
        }

        public void Configurate(string Config)
        {
            logger = new Logger("visualizer_log.txt");
            logger.log("Visualizer configurated");
            DropTemporalModel();
        }

        private void UpdateBB()
        {
            IntegrationComponent.m_broker.BlackBoard.LoadFromFile(@"BB2.xml");
        }

        private void ProcessOneTact()
        {
            Object res = new Object();
            logger.log("Processing model tact");
            m_broker.SendMessage("IntegrationComponent", "Model", "1", out res);
            logger.log("Sending message to TemporalReasoner");
            m_broker.SendMessage("IntegrationComponent", "TemporalReasoner", "TSolve", out res);

            UpdateBB();

            logger.log("Configuring ESKernel");
            m_broker.ConfigurateObject("ESKernel", "<config><FileName>TKBnewforAT.xml</FileName></config>");
            logger.log("Sending message to ESKernel");
            m_broker.SendMessage("IntegrationComponent", "ESKernel", "<message ProcName='TKnowledgeBase.ClearWorkMemory' />", out res);
            logger.log("Sending message to ESKernel");
            m_broker.SendMessage("IntegrationComponent", "ESKernel", "<message ProcName='TSolve' />", out res);
            
        }

        private void DropTemporalModel()
        {
            System.IO.File.Copy(@"BB2.xml", @"Model.xml", true);
        }

        public void ProcessMessage(string SenderName, string MessageText, System.Object OleVariant)
        {
            logger.log("Visualizer recieved a message: " + MessageText);
            XDocument message = XDocument.Parse(MessageText);
            string procName = message.Element("message").Attribute("ProcName").Value;
            if(procName.ToLower() == "ProcessTacts".ToLower())
            {
                int tactsNum = 1;

                XAttribute attrTactsNumber = message.Element("message").Attribute("TactsNumber");
                if(attrTactsNumber != null)
                    tactsNum = int.Parse(attrTactsNumber.Value);
                for (int i = 0; i < tactsNum; i++)
                    ProcessOneTact();
            }
            else if(procName.ToLower() == "DropTemporalModel".ToLower())
            {
                DropTemporalModel();
            }
            else if(procName.ToLower() == "ShowBB".ToLower())
            {
                m_broker.BlackBoard.ShowObjectTree();
            }
        }

        public void Stop()
        {
            Environment.Exit(0);
        }
    }
}
