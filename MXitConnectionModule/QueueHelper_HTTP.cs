using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using log4net;
using System.Reflection;
using System.Collections;
using System.Threading;
using MiscUtil.Threading;
using System.Diagnostics;
using System.Net;
using System.IO;
using System.Configuration;


namespace MXitConnectionModule
{
    public class QueueHelper_HTTP
    {
        private static volatile QueueHelper_HTTP instance;
        private static readonly ILog logger = LogManager.GetLogger(typeof(QueueHelper_HTTP));

        private Queue<HttpWebRequest> itemList = new Queue<HttpWebRequest>();
        private CustomThreadPool workerThreadPool;
        private AutoResetEvent resourceLockOut = new AutoResetEvent(true);

        private QueueHelper_HTTP()
        {
        }

        public static QueueHelper_HTTP Instance
        {
            get
            {
                if (instance == null)
                {
                    if (instance == null)
                        instance = new QueueHelper_HTTP();
                }

                return instance;
            }
        }

        public void processHTTPWebRequestImmediately(HttpWebRequest req)
        {
            using (HttpWebResponse response = (HttpWebResponse)req.GetResponse())
            {
                using (Stream responseStream = response.GetResponseStream())
                {
                    using (StreamReader streamIn = new StreamReader(responseStream))
                    {
                        String strResponse = streamIn.ReadToEnd();
                        streamIn.Close();
                    }
                    responseStream.Flush();
                    responseStream.Close();
                }
                response.Close();
            }
        }

        private void QueueHandler()
        {
            //this will receive a notification when there is a new HttpRequest in the thread.
            workerThreadPool = new CustomThreadPool();

            int minThreads = 1;
            int maxThreads = 20; //ConnectionConfig.queueHandlerMaxThreads_HTTP
            int processedQueueItemsCount = 0;

            workerThreadPool.SetMinMaxThreads(minThreads, maxThreads);

            logger.Info("[" + MethodBase.GetCurrentMethod().Name + "()] - HTTP Web Request thread started");

            while (1 == 1)
            {
                resourceLockOut.WaitOne();

                while (itemList.Count > 0)
                {
                    if ((itemList.Count > 100) && (itemList.Count % 1000 == 0))
                    {
                        logger.Error("[" + MethodBase.GetCurrentMethod().Name + "()] - http queue size: " + itemList.Count);
                    }

                    lock (itemList)
                    {
                        try
                        {
                            //Fetch item from front of queue and request a thread from thread pool to process it
                            HttpWebRequest req = itemList.Dequeue();

                            processHTTPWebRequestImmediately(req);

                            processedQueueItemsCount++;

                        }
                        catch (Exception e)
                        {
                            logger.Error("[" + MethodBase.GetCurrentMethod().Name + "()] - System Exception doing QUEUE Request: " + e.ToString());
                        }

                    }
                }
            }
        }


        public void StartQueueHandlers()
        {
            ThreadStart job = new ThreadStart(QueueHandler);
            Thread thread = new Thread(job);
            thread.Start();
        }

        public void QueueItem(HttpWebRequest httpWebRequest)
        {
            logger.Debug("[" + MethodBase.GetCurrentMethod().Name + "()] - START");

            lock (itemList)
            {
                itemList.Enqueue(httpWebRequest);
            }

            try
            {
                //ResourceLockOut.Release(1);
                resourceLockOut.Set();
            }
            catch (Exception e)
            {
                logger.Error("[" + MethodBase.GetCurrentMethod().Name + "()] - ResourceLock exception (Out)", e);
            }
        }

    }
}
