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
            if (path != null)
                _log = new StreamWriter(path);
            else
                _log = null;
        }

        ~Logger()
        {
            if(_log != null)
            {
                _log.Close();
                _log.Dispose();
            }
        }

        public void log(string text)
        {
            if (_log != null)
            {
                _log.WriteLine(text);
                _log.Flush();
            }
        }
    }

    public class IntegrationComponent : IIntegrationComponent
    {
        public static string m_name;
        public static IBroker m_broker;

        private static Logger logger;

        private static string bb;
        private static string temporalModel;
        private static string tkbNewForAt;
        private static string logfile;
        private static bool clearWorkMemory;

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
            bb = @"BB2.xml";
            temporalModel = @"Model.xml";
            tkbNewForAt = @"TKBnewforAT.xml";
            logfile = null;
            clearWorkMemory = true;

            XDocument config = XDocument.Parse(Config);
            XElement fileNameAT = config.Element("config").Element("FileNameAT");
            XElement fileBB = config.Element("config").Element("BB");
            XElement fileLog = config.Element("config").Element("Log");
            XElement fileTemporalModel = config.Element("config").Element("Model");
            XElement boolClearWorkMemory = config.Element("config").Element("ClearWorkMemory");

            if (fileNameAT != null)
                tkbNewForAt = fileNameAT.Value;
            if (fileBB != null)
                bb = fileBB.Value;
            if (fileLog != null)
                logfile = fileLog.Value;
            if (fileTemporalModel != null)
                temporalModel = fileTemporalModel.Value;
            if (boolClearWorkMemory != null)
                clearWorkMemory = bool.Parse(boolClearWorkMemory.Value);

            logger = new Logger(logfile);
            logger.log("Visualizer configurated");
            logger.log(String.Format("bb = {0}\r\ntemporalModel = {1}\r\ntkbNewFotAt = {2}", bb, temporalModel, tkbNewForAt));
            if (!File.Exists(bb))
            {
                using (StreamWriter bbCreator = new StreamWriter(bb))
                {
                    bbCreator.Write("<bb><wm><facts></facts></wm></bb>");
                }
            }
            DropTemporalModel();
        }

        private void UpdateBB()
        {
            logger.log(String.Format("Loading data from {0} to BlackBoard", bb));
            IntegrationComponent.m_broker.BlackBoard.LoadFromFile(bb);
        }

        private void ProcessOneTact()
        {
            Object res = new Object();
            System.Diagnostics.Stopwatch timer = System.Diagnostics.Stopwatch.StartNew();
            logger.log(String.Format("Tact begin, {0}ms", timer.ElapsedMilliseconds));
            logger.log(String.Format("Processing model tact, {0}ms", timer.ElapsedMilliseconds));
            m_broker.SendMessage("IntegrationComponent", "Model", "1", out res);
            logger.log(String.Format("Sending message to TemporalReasoner, {0}ms", timer.ElapsedMilliseconds));
            m_broker.SendMessage("IntegrationComponent", "TemporalReasoner", "TSolve", out res);

            UpdateBB();

            logger.log(String.Format("Configuring ESKernel with FileName {0}, {1}ms", tkbNewForAt, timer.ElapsedMilliseconds));
            m_broker.ConfigurateObject("ESKernel", String.Format("<config><FileName>{0}</FileName></config>", tkbNewForAt));
            if (clearWorkMemory)
            {
                logger.log(String.Format("Sending message to ESKernel ClearWorkMemory, {0}ms", timer.ElapsedMilliseconds));
                m_broker.SendMessage("IntegrationComponent", "ESKernel", "<message ProcName='TKnowledgeBase.ClearWorkMemory' />", out res);
            }
            logger.log(String.Format("Sending message to ESKernel TSolve, {0}ms", timer.ElapsedMilliseconds));
            m_broker.SendMessage("IntegrationComponent", "ESKernel", "<message ProcName='TSolve' />", out res);
            timer.Stop();
            logger.log(String.Format("Tact done, {0}ms", timer.ElapsedMilliseconds));
        }

        private void DropTemporalModel()
        {
            logger.log(String.Format("Dropping temporal model ({0}) to tact 0", temporalModel));
            System.IO.File.Copy(bb, temporalModel, true);
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
