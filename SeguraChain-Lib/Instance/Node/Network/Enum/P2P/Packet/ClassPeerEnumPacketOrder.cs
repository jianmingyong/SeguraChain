﻿using SeguraChain_Lib.Utility;

namespace SeguraChain_Lib.Instance.Node.Network.Enum.P2P.Packet
{
    public class ClassPeerPacketSetting
    {
        public static readonly byte[] PacketSeperatorBegin = "HELL".GetByteArray();
        public static readonly byte[] PacketSeperatorEnd = "FLAME".GetByteArray();
        public const int PacketSeperatorLength = 6;
        public const char PacketPeerSplitSeperator = '*';
        public const int PacketMaxLengthReceive = 10000000; // Maximum of 10000000 characters until to get the packet split seperator.
    }

    public enum ClassPeerEnumPacketSend
    {
        ASK_PEER_AUTH_KEYS = 0,
        ASK_PEER_LIST = 1,
        ASK_LIST_SOVEREIGN_UPDATE = 2,
        ASK_SOVEREIGN_UPDATE_FROM_HASH = 3,
        ASK_NETWORK_INFORMATION = 4,
        ASK_BLOCK_HEIGHT_INFORMATION = 5,
        ASK_BLOCK_DATA = 6,
        ASK_BLOCK_TRANSACTION_DATA = 7,
        ASK_BLOCK_TRANSACTION_DATA_BY_RANGE = 8,
        ASK_MEM_POOL_TRANSACTION_VOTE = 9,
        ASK_MEM_POOL_TRANSACTION_BY_BLOCK_HEIGHT_BROADCAST_MODE = 10,
        ASK_MEM_POOL_BLOCK_HEIGHT_LIST_BROADCAST_MODE = 11,
        ASK_MEM_POOL_TRANSACTION_BROADCAST_CONFIRMATION_RECEIVED = 12,
        ASK_MINING_SHARE_VOTE = 13,
        ASK_DISCONNECT_REQUEST = 14,
        ASK_KEEP_ALIVE = 15,
        ASK_MEM_POOL_BROADCAST_MODE = 16,
    }


    public enum ClassPeerEnumPacketResponse
    {
        NONE = 0,
        INVALID_PEER_PACKET = 1,
        INVALID_PEER_PACKET_SIGNATURE = 2,
        INVALID_PEER_PACKET_ENCRYPTION = 3,
        INVALID_PEER_PACKET_TIMESTAMP = 4,
        NOT_YET_SYNCED = 5,
        SEND_PEER_AUTH_KEYS = 6,
        SEND_PEER_LIST = 7,
        SEND_LIST_SOVEREIGN_UPDATE = 8,
        SEND_SOVEREIGN_UPDATE_FROM_HASH = 9,
        SEND_NETWORK_INFORMATION = 10,
        SEND_BLOCK_HEIGHT_INFORMATION = 11,
        SEND_BLOCK_DATA = 12,
        SEND_BLOCK_TRANSACTION_DATA = 13,
        SEND_BLOCK_TRANSACTION_DATA_BY_RANGE = 14,
        SEND_MEM_POOL_TRANSACTION_VOTE = 15,
        SEND_MINING_SHARE_VOTE = 16,
        SEND_DISCONNECT_CONFIRMATION = 17,
        SEND_MEM_POOL_BLOCK_HEIGHT_LIST_BROADCAST_MODE = 18,
        SEND_MEM_POOL_TRANSACTION_BY_BLOCK_HEIGHT_BROADCAST_MODE = 19,
        SEND_MEM_POOL_BROADCAST_RESPONSE = 20,
        SEND_MEM_POOL_END_TRANSACTION_BY_BLOCK_HEIGHT_BROADCAST_MODE = 21,
        SEND_MISSING_AUTH_KEYS = 22
    }
}
