//Drop this file in the same project as your Controller.cs file

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using log4net;
using System.Reflection;
using System.Collections;
using System.Threading;
using System.Diagnostics;
using System.Net;
using System.IO;
using System.Configuration;
using MXit.Messaging;
using MiscUtil.Threading;
using MXitConnectionModule;

namespace YOURAPPNAMESPACE
{
    public class QueueHelper_IncomingMessage
    {
        private static volatile QueueHelper_IncomingMessage instance;
        private static readonly ILog logger = LogManager.GetLogger(typeof(QueueHelper_IncomingMessage));

        private Queue<MessageReceived> itemList = new Queue<MessageReceived>();
        private CustomThreadPool workerThreadPool;
        private AutoResetEvent resourceLockOut = new AutoResetEvent(true);

        private QueueHelper_IncomingMessage()
        {
        }

        public static QueueHelper_IncomingMessage Instance
        {
            get
            {
                if (instance == null)
                {
                    if (instance == null)
                        instance = new QueueHelper_IncomingMessage();
                }

                return instance;
            }
        }

        public delegate void incomingDelegate(MessageReceived messageReceived);

        public void processRequestImmediately(MessageReceived item)
        {
            workerThreadPool.AddWorkItem(new incomingDelegate(Controller.Instance.ProcessMessage), item);
        }

        private void QueueHandler()
        {
            //this will receive a notification when there is a new HttpRequest in the thread.
            workerThreadPool = new CustomThreadPool();

            int minThreads = 1;
            int maxThreads = ConnectionConfig.QueueHelperMaxThreads_IncomingMessage;

            workerThreadPool.SetMinMaxThreads(minThreads, maxThreads);

            logger.Info(MethodBase.GetCurrentMethod().Name + "() - queue handler thread started");

            while (true)
            {
                resourceLockOut.WaitOne();

                while (itemList.Count > 0 && !ConnectionManager.Instance.IsReconnecting)
                {
                    if ((itemList.Count > 100) && (itemList.Count % 1000 == 0))
                    {
                        logger.Info(MethodBase.GetCurrentMethod().Name + "() - queue size: " + itemList.Count);
                    }

                    lock (itemList)
                    {
                        try
                        {
                            //Fetch item from front of queue and request a thread from thread pool to process it
                            MessageReceived item = itemList.Dequeue();

                            processRequestImmediately(item);
                        }
                        catch (Exception e)
                        {
                            logger.Error(MethodBase.GetCurrentMethod().Name + "() - System Exception doing QUEUE Request: " + e.ToString());
                        }

                    }
                }
            }
        }


        public void StartQueueHandlers()
        {
            logger.Debug(MethodBase.GetCurrentMethod().Name + "() - START");

            Console.Write(DateTime.Now.ToString() + " Starting Queue Handler [Incoming Messages]...");

            ThreadStart job = new ThreadStart(QueueHandler);
            Thread thread = new Thread(job);
            thread.Start();

            Console.WriteLine(" OK");

            logger.Debug(MethodBase.GetCurrentMethod().Name + "() - END");
        }

        public bool EnqueueItem(MessageReceived item)
        {
            logger.Debug(MethodBase.GetCurrentMethod().Name + "() - START");
            bool success = false;

            try
            {

                lock (itemList)
                {
                    itemList.Enqueue(item);
                }
                success = true;

                try
                {
                    resourceLockOut.Set();
                }
                catch (Exception e)
                {
                    logger.Error(MethodBase.GetCurrentMethod().Name + "() - ResourceLock exception (Out)", e);
                }
            }
            catch (Exception ex)
            {
                logger.Error(MethodBase.GetCurrentMethod().Name + "() - Enqueue exception: ", ex);
                success = false;
            }

            logger.Debug(MethodBase.GetCurrentMethod().Name + "() - END");
            return success;
        }

        public int getQueueSize()
        {
            return itemList.Count;
        }

    }
}
