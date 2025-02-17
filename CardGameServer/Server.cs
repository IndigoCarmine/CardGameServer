﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

/// <summary>
/// https://webbibouroku.com/Blog/Article/socket-server から引用また、一部改変
/// </summary>


namespace CardGameServer
{
    abstract public class Server
    {
        // スレッド待機用
        private ManualResetEvent AllDone = new ManualResetEvent(false);

        // サーバーのエンドポイント
        public IPEndPoint IPEndPoint { get; }

        // 接続中のクライアント(スレッドセーフコレクション)
        public SynchronizedCollection<Socket> ClientSockets { get; } = new SynchronizedCollection<Socket>();



        IPAddress ip;

        public Server(int port)
        {
            foreach (IPAddress address in Dns.GetHostAddresses(Dns.GetHostName()))
            {
                ip = address;
            }

            this.IPEndPoint = new IPEndPoint(ip, port);
        }

        // サーバー起動
        public void Run()
        {
            using (var listenerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                // Ctrl+Cが押された場合はソケットを閉じる
                Console.CancelKeyPress += (sender, args) =>
                {
                    foreach (var clientSocket in this.ClientSockets) clientSocket?.Close();
                };

                // ソケットをアドレスにバインドする
                listenerSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                listenerSocket.Bind(this.IPEndPoint);

                // 接続待機開始
                listenerSocket.Listen(10);
                Console.WriteLine($"サーバーを起動しました ... [{listenerSocket.LocalEndPoint}]");
                Console.WriteLine($"Ctrl + C で中止します ...");

                // 接続待機のループ
                while (true)
                {
                    AllDone.Reset();
                    listenerSocket.BeginAccept(new AsyncCallback(AcceptCallback), listenerSocket);
                    AllDone.WaitOne();
                }
            }
        }

        // 接続受付時のコールバック処理
        private void AcceptCallback(IAsyncResult asyncResult)
        {
            // 待機スレッドが進行するようにシグナルをセット
            AllDone.Set();

            // ソケットを取得
            var listenerSocket = asyncResult.AsyncState as Socket;
            var clientSocket = listenerSocket.EndAccept(asyncResult);

            // 接続中のクライアントを追加
            ClientSockets.Add(clientSocket);
            Console.WriteLine($"接続: {clientSocket.RemoteEndPoint}");
            

            // StateObjectを作成
            var state = new StateObject();
            state.ClientSocket = clientSocket;
            OnConnectNewClient(state);


            // 受信時のコードバック処理を設定
            clientSocket.BeginReceive(state.Buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReceiveCallback), state);
        }

        private void ReceiveCallback(IAsyncResult asyncResult)
        {
            // StateObjectとクライアントソケットを取得
            var state = asyncResult.AsyncState as StateObject;
            var clientSocket = state.ClientSocket;

            // クライアントソケットから受信データを取得終了
            int bytes = clientSocket.EndReceive(asyncResult);

            if (bytes > 0)
            {
                // 受信した文字列を表示
                var content = Encoding.UTF8.GetString(state.Buffer, 0, bytes);
                Console.WriteLine($"受信データ: {content} [{state.ClientSocket.RemoteEndPoint}]");

                OnReceive(state,content);
                
                // 受信時のコードバック処理を再設定
                clientSocket.BeginReceive(state.Buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReceiveCallback), state);
            }
            else
            {
                // 0バイトデータの受信時は、切断されたとき?
                clientSocket.Close();
                this.ClientSockets.Remove(clientSocket);
                OnDisconnect(state);
            }
        }

        // クライアントへのメッセージ送信処理
        protected void Send(Socket clientSocket, String data)
        {
            // 受信データをUTF8文字列に変換し送信
            var bytes = Encoding.UTF8.GetBytes(data);
            clientSocket.BeginSend(bytes, 0, bytes.Length, 0, new AsyncCallback(SendCallback), clientSocket);
        }

        // 送信時のコールバック処理
        private static void SendCallback(IAsyncResult asyncResult)
        {
            try
            {
                // クライアントソケットへのデータ送信処理を完了する
                var clientSocket = asyncResult.AsyncState as Socket;
                var byteSize = clientSocket.EndSend(asyncResult);
                Console.WriteLine($"送信結果: {byteSize}バイト [{clientSocket.RemoteEndPoint}]");
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        // 全クライアントへの送信処理
        protected void SendAllClient(string data)
        {
            foreach (var clientSocket in this.ClientSockets)
            {
                Send(clientSocket, data);
            }
        }

        abstract protected void OnConnectNewClient(StateObject stateObject);
        abstract protected void OnDisconnect(StateObject stateObject);
        abstract protected void OnReceive(StateObject stateObject, string content);
    }

    public class StateObject
    {
        public Socket ClientSocket { get; set; }
        public const int BufferSize = 1024;
        public byte[] Buffer { get; } = new byte[BufferSize];
    }
}
