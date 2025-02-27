﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using SeguraChain_Lib.Instance.Node.Network.Database;
using SeguraChain_Lib.Instance.Node.Network.Database.Manager;
using SeguraChain_Lib.Instance.Node.Network.Enum.P2P.Packet;
using SeguraChain_Lib.Instance.Node.Network.Services.P2P.Broadcast;
using SeguraChain_Lib.Instance.Node.Network.Services.P2P.Sync.Packet;
using SeguraChain_Lib.Instance.Node.Network.Services.P2P.Sync.Packet.Model;
using SeguraChain_Lib.Instance.Node.Network.Services.P2P.Sync.Packet.SubPacket.Request;
using SeguraChain_Lib.Instance.Node.Setting.Object;
using SeguraChain_Lib.Log;
using SeguraChain_Lib.Other.Object.List;
using SeguraChain_Lib.Other.Object.Network;
using SeguraChain_Lib.Utility;
using static SeguraChain_Lib.Other.Object.Network.ClassCustomSocket;

namespace SeguraChain_Lib.Instance.Node.Network.Services.P2P.Sync.ClientSync.ClientConnect.Object
{
    public class ClassPeerNetworkClientSyncObject : IDisposable
    {
        /// <summary>
        /// Tcp info and tcp client object.
        /// </summary>
        private ClassCustomSocket _peerSocketClient;
        public bool PeerConnectStatus => _peerSocketClient != null ? _peerSocketClient.IsConnected() : false;

        /// <summary>
        /// Peer informations.
        /// </summary>
        public string PeerIpTarget;
        public int PeerPortTarget;
        public string PeerUniqueIdTarget;
     

        /// <summary>
        /// Packet received.
        /// </summary>
        public ClassPeerPacketRecvObject PeerPacketReceived;
        public ClassPeerEnumPacketResponse PeerPacketTypeReceived;
        public bool PeerPacketReceivedStatus;

        /// <summary>
        /// Peer task status.
        /// </summary>
        public bool PeerTaskStatus;
        private bool _peerTaskKeepAliveStatus;
        private CancellationTokenSource _peerCancellationTokenMain;
        private CancellationTokenSource _peerCancellationTokenKeepAlive;
        private CancellationTokenSource _peerCancellationTokenTaskListenPeerPacketResponse;


        /// <summary>
        /// Network settings.
        /// </summary>
        private ClassPeerNetworkSettingObject _peerNetworkSetting;
        private ClassPeerFirewallSettingObject _peerFirewallSettingObject;

        /// <summary>
        /// Specifications of the connection opened.
        /// </summary>
        public ClassPeerEnumPacketResponse PacketResponseExpected;
        private DisposableList<ClassReadPacketSplitted> listPacketReceived;
        private bool _keepAlive;


        #region Dispose functions

        ~ClassPeerNetworkClientSyncObject()
        {
            Dispose(true);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        // Protected implementation of Dispose pattern.
        protected virtual void Dispose(bool disposing)
        {
            PeerTaskStatus = false;
            CleanUpTask();
            DisconnectFromTarget();
        }

        #endregion

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="peerIpTarget"></param>
        /// <param name="peerPort"></param>
        /// <param name="peerUniqueId"></param>
        /// <param name="peerNetworkSetting"></param>
        /// <param name="peerFirewallSettingObject"></param>
        public ClassPeerNetworkClientSyncObject(string peerIpTarget, int peerPort, string peerUniqueId, ClassPeerNetworkSettingObject peerNetworkSetting, ClassPeerFirewallSettingObject peerFirewallSettingObject)
        {
            PeerIpTarget = peerIpTarget;
            PeerPortTarget = peerPort;
            PeerUniqueIdTarget = peerUniqueId;
            _peerNetworkSetting = peerNetworkSetting;
            _peerFirewallSettingObject = peerFirewallSettingObject;
        }

        /// <summary>
        /// Attempt to send a packet to a peer target.
        /// </summary>
        /// <param name="packetSendObject"></param>
        /// <param name="cancellation"></param>
        /// <param name="packetResponseExpected"></param>
        /// <param name="keepAlive"></param>
        /// <param name="broadcast"></param>
        /// <returns></returns>
        public async Task<bool> TrySendPacketToPeerTarget(ClassPeerPacketSendObject packetSendObject, bool toSignAndEncrypt, int peerPort, string peerUniqueId, CancellationTokenSource cancellation, ClassPeerEnumPacketResponse packetResponseExpected, bool keepAlive, bool broadcast)
        {

            #region Clean up and cancel previous task.

            CleanUpTask();

            #endregion

            _peerCancellationTokenMain = CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token);

            if (toSignAndEncrypt)
            {
                packetSendObject = await ClassPeerNetworkBroadcastShortcutFunction.BuildSignedPeerSendPacketObject(packetSendObject, PeerIpTarget, peerUniqueId, false, _peerNetworkSetting, _peerCancellationTokenMain);
               
                if (packetSendObject == null ||
                    packetSendObject.PacketContent.IsNullOrEmpty(false, out _) ||
                    packetSendObject.PacketHash.IsNullOrEmpty(false, out _) ||
                    packetSendObject.PacketSignature.IsNullOrEmpty(false, out _) ||
                    packetSendObject.PublicKey.IsNullOrEmpty(false, out _))
                    return false;
            }


            byte[] packetData = packetSendObject.GetPacketData();

            if (packetData == null)
                return false;

            

            #region Init the client sync object.

            PacketResponseExpected = packetResponseExpected;
            _keepAlive = keepAlive;
            PeerPortTarget = peerPort;
            PeerUniqueIdTarget = peerUniqueId;

            #endregion

            #region Check the current connection status opened to the target.

            if (!PeerConnectStatus || !keepAlive)
            {
                DisconnectFromTarget();


                if (!await DoConnection())
                {
                    ClassLog.WriteLine("Failed to connect to peer " + PeerIpTarget + ":" + PeerPortTarget, ClassEnumLogLevelType.LOG_LEVEL_PEER_TASK_SYNC, ClassEnumLogWriteLevel.LOG_WRITE_LEVEL_LOWEST_PRIORITY);
                    ClassPeerCheckManager.InputPeerClientAttemptConnect(PeerIpTarget, PeerUniqueIdTarget, _peerNetworkSetting, _peerFirewallSettingObject);
                    DisconnectFromTarget();
                    return false;
                }
            }

            #endregion

            #region Send packet and wait packet response.


            if (!await SendPeerPacket(packetData, _peerCancellationTokenMain))
            {
                ClassPeerCheckManager.InputPeerClientNoPacketConnectionOpened(PeerIpTarget, PeerUniqueIdTarget, _peerNetworkSetting, _peerFirewallSettingObject);
                DisconnectFromTarget();
                return false;
            }
            else
                return broadcast ? true : await WaitPacketExpected();

            #endregion

        }

        #region Initialize connection functions.

        /// <summary>
        /// Clean up the task.
        /// </summary>
        private void CleanUpTask()
        {
            PeerPacketTypeReceived = ClassPeerEnumPacketResponse.NONE;
            PeerPacketReceived = null;
            PeerPacketReceivedStatus = false;
            CancelKeepAlive();
            try
            {
                if (_peerCancellationTokenMain != null)
                {
                    if (!_peerCancellationTokenMain.IsCancellationRequested)
                        _peerCancellationTokenMain.Cancel();
                }
            }
            catch
            {
                // Ignored, if already cancelled.
            }
        }


        /// <summary>
        /// Do connection.
        /// </summary>
        /// <returns></returns>
        private async Task<bool> DoConnection()
        {
            bool successConnect = false;

            long timestampEnd = TaskManager.TaskManager.CurrentTimestampMillisecond + (_peerNetworkSetting.PeerMaxDelayToConnectToTarget * 1000);

            var peerCancellationTokenDoConnection = CancellationTokenSource.CreateLinkedTokenSource(_peerCancellationTokenMain.Token, new CancellationTokenSource(_peerNetworkSetting.PeerMaxDelayToConnectToTarget * 1000).Token);

            TaskManager.TaskManager.InsertTask(new Action(async () =>
            {
                while (!successConnect && timestampEnd > TaskManager.TaskManager.CurrentTimestampMillisecond)
                {
                    try
                    {
                        if (peerCancellationTokenDoConnection.IsCancellationRequested)
                            break;

                        _peerSocketClient?.Kill(SocketShutdown.Both);
                        
                        _peerSocketClient = new ClassCustomSocket(new Socket(ClassUtility.GetAddressFamily(PeerIpTarget), SocketType.Stream, ProtocolType.Tcp), false);

                        if (await _peerSocketClient.ConnectAsync(PeerIpTarget, PeerPortTarget))
                            successConnect = _peerSocketClient.IsConnected();
                        else _peerSocketClient?.Kill(SocketShutdown.Both);

                    }
                    catch
                    {
                        // Ignored, catch the exception once the attempt to connect to a peer failed.
                    }


                    await Task.Delay(10);
                }


            }), timestampEnd, peerCancellationTokenDoConnection);


            while (!successConnect)
            {
                if (timestampEnd < TaskManager.TaskManager.CurrentTimestampMillisecond ||
                    peerCancellationTokenDoConnection.IsCancellationRequested )
                    break;

                await Task.Delay(10);

            }

            if (timestampEnd < TaskManager.TaskManager.CurrentTimestampMillisecond || peerCancellationTokenDoConnection.IsCancellationRequested && !successConnect)
                Debug.WriteLine("timeout to connect.");

            return successConnect;
        }


        /// <summary>
        /// Wait the packet expected.
        /// </summary>
        /// <returns></returns>
        private async Task<bool> WaitPacketExpected()
        {
            CancelHandlePacket();

            _peerCancellationTokenTaskListenPeerPacketResponse = CancellationTokenSource.CreateLinkedTokenSource(_peerCancellationTokenMain.Token);

            PeerTaskStatus = true;

            TaskWaitPeerPacketResponse();

            while (!PeerPacketReceivedStatus && !_peerCancellationTokenTaskListenPeerPacketResponse.IsCancellationRequested &&
                    PeerConnectStatus && PeerTaskStatus &&
                    PeerPacketReceived == null)
            {
                await Task.Delay(10);
            }

            if (PeerPacketReceived == null)
            {
                ClassLog.WriteLine("Peer " + PeerIpTarget + " don't send a response to the packet sent.", ClassEnumLogLevelType.LOG_LEVEL_PEER_TASK_SYNC, ClassEnumLogWriteLevel.LOG_WRITE_LEVEL_LOWEST_PRIORITY, true);
                DisconnectFromTarget();
                return false;
            }
            else
            {
                if (!_keepAlive)
                    DisconnectFromTarget();
                else // Enable keep alive.
                    TaskEnablePeerPacketKeepAlive();
            }

            return true;
        }

        #endregion

        #region Wait packet to receive functions.

        /// <summary>
        /// Task in waiting a packet of response sent by the peer target.
        /// </summary>
        private void TaskWaitPeerPacketResponse()
        {


            TaskManager.TaskManager.InsertTask(new Action(async () =>
            {

                listPacketReceived?.Clear();

                using (listPacketReceived = new DisposableList<ClassReadPacketSplitted>(false, 0, new List<ClassReadPacketSplitted>()
                {
                    new ClassReadPacketSplitted()
                }))
                {
                    while (PeerTaskStatus && PeerConnectStatus)
                    {
                        using (ReadPacketData readPacketData = await _peerSocketClient.TryReadPacketData(_peerNetworkSetting.PeerMaxPacketBufferSize, _peerCancellationTokenTaskListenPeerPacketResponse))
                        {

                            ClassPeerCheckManager.UpdatePeerClientLastPacketReceived(PeerIpTarget, PeerUniqueIdTarget, TaskManager.TaskManager.CurrentTimestampSecond);

                            if (!readPacketData.Status)
                                break;

                            #region Compile the packet.

                            listPacketReceived = ClassUtility.GetEachPacketSplitted(readPacketData.Data, listPacketReceived, _peerCancellationTokenTaskListenPeerPacketResponse);

                            #endregion

                            int countCompleted = listPacketReceived.GetList.Count(x => x.Complete);

                            if (countCompleted == 0)
                                continue;

                            if (listPacketReceived[listPacketReceived.Count - 1].Used ||
                            !listPacketReceived[listPacketReceived.Count - 1].Complete ||
                             listPacketReceived[listPacketReceived.Count - 1].Packet == null ||
                             listPacketReceived[listPacketReceived.Count - 1].Packet.Length == 0)
                                continue;

                            byte[] base64Packet = null;
                            bool failed = false;

                            listPacketReceived[listPacketReceived.Count - 1].Used = true;

                            try
                            {
                                base64Packet = Convert.FromBase64String(listPacketReceived[listPacketReceived.Count - 1].Packet);
                            }
                            catch
                            {
                                failed = true;
                            }

                            listPacketReceived[listPacketReceived.Count - 1].Packet.Clear();

                            if (failed)
                                continue;

                            ClassPeerPacketRecvObject peerPacketReceived = new ClassPeerPacketRecvObject(base64Packet, out bool status);

                            if (!status)
                            { 
#if DEBUG
                                Debug.WriteLine("Can't build packet data from: " + _peerSocketClient.GetIp);
#endif
                                break;
                            };

                            PeerPacketTypeReceived = peerPacketReceived.PacketOrder;

                            if (peerPacketReceived.PacketOrder == PacketResponseExpected)
                            {
                                PeerPacketReceivedStatus = true;

                                if (ClassPeerDatabase.ContainsPeer(PeerIpTarget, PeerUniqueIdTarget))
                                    ClassPeerDatabase.DictionaryPeerDataObject[PeerIpTarget][PeerUniqueIdTarget].PeerTimestampSignatureWhitelist = peerPacketReceived.PeerLastTimestampSignatureWhitelist;

                                PeerPacketReceived = peerPacketReceived;

                            }

                            else
                            {
                                PeerPacketReceivedStatus = peerPacketReceived.PacketOrder == ClassPeerEnumPacketResponse.SEND_MISSING_AUTH_KEYS;
#if DEBUG
                                if (!PeerPacketReceivedStatus)
                                    Debug.WriteLine("Failed, the packet order expected is invalid: " + peerPacketReceived.PacketOrder + "/" + PacketResponseExpected);
#endif
                            }
                            break;
                        }
                    }
                }

                PeerTaskStatus = false;


            }), 0, _peerCancellationTokenTaskListenPeerPacketResponse, null);

        }

        #endregion

        #region Enable Keep alive functions.

        /// <summary>
        /// Enable a task who send a packet of keep alive to the peer target.
        /// </summary>
        private void TaskEnablePeerPacketKeepAlive()
        {

            CancelKeepAlive();

            var peerObject = ClassPeerDatabase.GetPeerObject(PeerIpTarget, PeerUniqueIdTarget);

            if (peerObject != null)
            {
                _peerTaskKeepAliveStatus = true;

                _peerCancellationTokenKeepAlive = new CancellationTokenSource();

                TaskManager.TaskManager.InsertTask(new Action(async () =>
                {

                    var sendObject = new ClassPeerPacketSendObject(_peerNetworkSetting.PeerUniqueId, peerObject.PeerInternPublicKey, peerObject.PeerClientLastTimestampPeerPacketSignatureWhitelist)
                    {
                        PacketOrder = ClassPeerEnumPacketSend.ASK_KEEP_ALIVE,
                        PacketContent = ClassUtility.SerializeData(new ClassPeerPacketAskKeepAlive()
                        {
                            PacketTimestamp = TaskManager.TaskManager.CurrentTimestampSecond
                        }),
                    };


                    while (PeerConnectStatus && _peerTaskKeepAliveStatus)
                    {
                        try
                        {

                            sendObject.PacketContent = ClassUtility.SerializeData(new ClassPeerPacketAskKeepAlive()
                            {
                                PacketTimestamp = TaskManager.TaskManager.CurrentTimestampSecond
                            });

                            if (!await SendPeerPacket(sendObject.GetPacketData(), _peerCancellationTokenKeepAlive))
                            {
                                _peerTaskKeepAliveStatus = false;
                                break;
                            }

                            await Task.Delay(5 * 1000);
                        }
                        catch (Exception e)
                        {
                            if (e is SocketException || e is TaskCanceledException)
                            {
                                _peerTaskKeepAliveStatus = false;
                                break;
                            }
                        }

                    }

                }), 0, _peerCancellationTokenKeepAlive, null);
            }
        }



        #endregion

        #region Manage TCP Connection.

        /// <summary>
        /// Send a packet to the peer target.
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="cancellation"></param>
        /// <returns></returns>
        private async Task<bool> SendPeerPacket(byte[] packet, CancellationTokenSource cancellation)
        {
            string packetData = Convert.ToBase64String(packet) + ClassPeerPacketSetting.PacketPeerSplitSeperator.ToString();
            return await _peerSocketClient.TrySendSplittedPacket(packetData.GetByteArray(), cancellation, _peerNetworkSetting.PeerMaxPacketSplitedSendSize);
        }

        private void CancelHandlePacket()
        {
            try
            {
                if (_peerCancellationTokenTaskListenPeerPacketResponse != null)
                {
                    if (!_peerCancellationTokenTaskListenPeerPacketResponse.IsCancellationRequested)
                        _peerCancellationTokenTaskListenPeerPacketResponse.Cancel();
                }
            }
            catch
            {

            }

            PeerTaskStatus = false;
            PeerPacketReceivedStatus = false;
            PeerPacketReceived = null;
            PeerPacketTypeReceived = ClassPeerEnumPacketResponse.NONE;
        }

        /// <summary>
        /// Cancel keep alive.
        /// </summary>
        private void CancelKeepAlive()
        {

            try
            {
                if (_peerCancellationTokenKeepAlive != null)
                {
                    if (!_peerCancellationTokenKeepAlive.IsCancellationRequested)
                        _peerCancellationTokenKeepAlive.Cancel();
                }
            }
            catch
            {

            }

            _peerTaskKeepAliveStatus = false;

        }

        /// <summary>
        /// Disconnect from target.
        /// </summary>
        public void DisconnectFromTarget()
        {
            listPacketReceived?.Clear();
            _peerSocketClient?.Kill(SocketShutdown.Both);
        }



        #endregion
    }
}
