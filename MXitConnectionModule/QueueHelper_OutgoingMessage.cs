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


namespace MXitConnectionModule
{
    public class QueueHelper_OutgoingMessage
    {
        private static volatile QueueHelper_OutgoingMessage instance;
        private static readonly ILog logger = LogManager.GetLogger(typeof(QueueHelper_OutgoingMessage));

        private Queue<IMessageToSend> itemList = new Queue<IMessageToSend>();
        private CustomThreadPool workerThreadPool;
        private AutoResetEvent resourceLockOut = new AutoResetEvent(true);

        private QueueHelper_OutgoingMessage()
        {
        }

        public static QueueHelper_OutgoingMessage Instance
        {
            get
            {
                if (instance == null)
                {
                    if (instance == null)
                        instance = new QueueHelper_OutgoingMessage();
                }

                return instance;
            }
        }

        public delegate bool outgoingDelegate(IMessageToSend messageToSend);

        public void addRequestToWorkerPool(IMessageToSend item)
        {
            workerThreadPool.AddWorkItem(new outgoingDelegate(ConnectionManager.Instance.SendMessage), item);
        }

        private void QueueHandler()
        {
            logger.Debug(MethodBase.GetCurrentMethod().Name + "() - START");

            //this will receive a notification when there is a new HttpRequest in the thread.
            workerThreadPool = new CustomThreadPool();

            int minThreads = 1;
            int maxThreads = ConnectionConfig.QueueHelperMaxThreads_OutgoingMessage;

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

                    IMessageToSend item = null;

                    lock (itemList)
                    {
                        try
                        {
                            //Fetch item from front of queue and request a thread from thread pool to process it
                            item = itemList.Dequeue();
                           
                        }
                        catch (Exception e)
                        {
                            logger.Error(MethodBase.GetCurrentMethod().Name + "() - System Exception doing QUEUE Request: " + e.ToString());
                        }

                    }

                    if (item != null)
                        addRequestToWorkerPool(item);
                }
            }

            logger.Debug(MethodBase.GetCurrentMethod().Name + "() - END");
        }


        public void StartQueueHandlers()
        {
            logger.Debug(MethodBase.GetCurrentMethod().Name + "() - START");

            Console.Write(DateTime.Now.ToString() + " Starting Queue Handler [Outgoing Messages]...");

            ThreadStart job = new ThreadStart(QueueHandler);
            Thread thread = new Thread(job);
            thread.Start();

            Console.WriteLine(" OK");

            logger.Debug(MethodBase.GetCurrentMethod().Name + "() - END");
        }

        public bool EnqueueItem(IMessageToSend item)
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

        public int getWorkerPoolQueueSize()
        {
            return this.workerThreadPool.QueueLength;
        }

        public int getWorkerPoolThreadCount()
        {
            return this.workerThreadPool.TotalThreads;
        }
    }
}
