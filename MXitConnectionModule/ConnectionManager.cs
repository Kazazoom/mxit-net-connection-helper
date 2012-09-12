using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using MXit;
using MXit.User;
using MXit.Messaging;
using MXit.Billing;
using MXit.Messaging.MessageElements;

namespace MXitConnectionModule
{
    public sealed class ConnectionManager
    {
        private static volatile ConnectionManager instance;
        private static object syncRoot = new Object();

        public ConnectionManager()
        {
        }

        /// <summary>
        /// The instance through which the Singleton is accessed
        /// </summary>
        public static ConnectionManager Instance
        {
            get
            {
                if (instance == null)
                {
                    lock (syncRoot)
                    {
                        if (instance == null)
                            instance = new ConnectionManager();
                    }
                }

                return instance;
            }
        }

        public bool IsRunning
        {
            get
            {
                return SharpConnectionHelperSingleton.Instance.IsRunning;
            }
        }


        public bool IsReconnecting
        {
            get
            {
                return SharpConnectionHelperSingleton.Instance.IsReconnecting;
            }
        }

        public bool SendMessage(IMessageToSend message)
        {
            return SharpConnectionHelperSingleton.Instance.SendMessage(message);
        }

        public void RequestPayment(PaymentRequest paymentRequest)
        {
            SharpConnectionHelperSingleton.Instance.RequestPayment(paymentRequest);
        }

        public UserInfo GetUser(String userID)
        {
            return SharpConnectionHelperSingleton.Instance.GetUser(userID);
        }

        public long? ConfirmPayment(int vendorId, String txRef)
        {
            return SharpConnectionHelperSingleton.Instance.ConfirmPayment(vendorId, txRef);
        }

        public void InitializeConnection(string appName, string appPassword, ExternalAppAPI.CommsCallback callback)
        {
            SharpConnectionHelperSingleton.Instance.InitializeConnection(appName, appPassword, callback);

            QueueHelper_OutgoingMessage.Instance.StartQueueHandlers();
        }

        public void Connect(ExternalAppAPI.CommsCallback callback)
        {
            SharpConnectionHelperSingleton.Instance.Connect(callback);
        }

        public void ReConnectFull()
        {
            SharpConnectionHelperSingleton.Instance.ReConnectFull();
        }

        public void Disconnect()
        {
            SharpConnectionHelperSingleton.Instance.Disconnect();
        }

        public void RedirectRequest(MXit.Navigation.RedirectRequest redirectRequest)
        {
            SharpConnectionHelperSingleton.Instance.RedirectRequest(redirectRequest);
        }

        public ImageStripReference RegisterImageStrip(String name, Bitmap image, int frameWidth, int frameHeight, int layer)
        {
            return SharpConnectionHelperSingleton.Instance.RegisterImageStrip(name, image, frameWidth, frameHeight, layer);
        }

        public bool EnqueueMessageToSend(MessageToSend item)
        {
            return QueueHelper_OutgoingMessage.Instance.EnqueueItem(item);
        }
    }
}
