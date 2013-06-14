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
    public class ThreadPool_SendRESTMessage
    {
        private static volatile ThreadPool_SendRESTMessage instance;
        private static readonly ILog logger = LogManager.GetLogger(typeof(ThreadPool_SendRESTMessage));

        private CustomThreadPool workerThreadPool;
        private AutoResetEvent resourceLockOut = new AutoResetEvent(true);

        private ThreadPool_SendRESTMessage()
        {
            initThreadPool();
        }

        public static ThreadPool_SendRESTMessage Instance
        {
            get
            {
                if (instance == null)
                {
                    if (instance == null)
                        instance = new ThreadPool_SendRESTMessage();
                }

                return instance;
            }
        }

        public delegate bool outgoingDelegate(RESTMessageToSend rMessageToSend);

        //Public method exposed for starting the thread pools
        private void initThreadPool()
        {
            logger.Debug(MethodBase.GetCurrentMethod().Name + "() - START");

            Console.Write(DateTime.Now.ToString() + " Creating Thread Pool: ThreadPool_SendRESTMessage... ");

            //Create the thread pool
            workerThreadPool = new CustomThreadPool();

            int minThreads = 1;
            int maxThreads = ConnectionConfig.QueueHelperMaxThreads_OutgoingRESTMessage;

            workerThreadPool.SetMinMaxThreads(minThreads, maxThreads);

            Console.WriteLine(" OK");

            logger.Debug(MethodBase.GetCurrentMethod().Name + "() - END");
        }

        public bool EnqueueItem(RESTMessageToSend item)
        {
            logger.Debug(MethodBase.GetCurrentMethod().Name + "() - START");
            bool success = false;

            try
            {
                workerThreadPool.AddWorkItem(new outgoingDelegate(this.ThreadDoMethod), item);
            }
            catch (Exception ex)
            {
                logger.Error(MethodBase.GetCurrentMethod().Name + "() - Enqueue exception: ", ex);
                success = false;
            }

            logger.Debug(MethodBase.GetCurrentMethod().Name + "() - END");
            return success;
        }

        public int getWorkerPoolQueueSize()
        {
            return this.workerThreadPool.QueueLength;
        }

        public int getWorkerPoolThreadCount()
        {
            return this.workerThreadPool.TotalThreads;
        }

        public String getInfoString()
        {
            return
                (
                this.workerThreadPool.MinThreads + " | " +
                this.workerThreadPool.MaxThreads + " | w:" +
                this.workerThreadPool.WorkingThreads + " | t:" +
                this.workerThreadPool.TotalThreads + " | q:" +
                this.workerThreadPool.QueueLength
                );
        }

        private bool ThreadDoMethod(RESTMessageToSend rMessageToSend)
        {
            bool sendMessageSuccess = false;

            try
            {
                //Print out where we are in the queue:
                int queueItemCount = workerThreadPool.QueueLength;
                logger.Info(MethodBase.GetCurrentMethod().Name + "() - queue size: " + queueItemCount);
                Console.WriteLine(DateTime.Now.ToString() + " queue size: " + queueItemCount);

                //Do the work:
                sendMessageSuccess = RESTConnectionHelper.Instance.SendMessage(rMessageToSend);

                if (!sendMessageSuccess)
                {
                    logger.Error(MethodBase.GetCurrentMethod().Name + "() - Problem: Couldn't send REST broadcast message...");
                    Console.WriteLine(DateTime.Now.ToString() + " ERROR: Couldn't send REST broadcast message...: ");
                }
            }
            catch (Exception e)
            {
                logger.Error(MethodBase.GetCurrentMethod().Name + "() - Exception: " + e.ToString());
                sendMessageSuccess = false;
            }

            return sendMessageSuccess;
        } //ThreadDoMethod

    }
}
