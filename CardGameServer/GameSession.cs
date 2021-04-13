using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace CardGameServer
{
    public class GameSession : Server
    {

        List<int> Deck = new List<int>();
        int TopDrawCard = 40;//初期設定で裏面にしておく

        List<Player> playerList = new List<Player>();

        public GameSession(int port) : base(port)
        {
            //数字カードを追加
            for(int i = 0; 40> i; i++)
            {
                Deck.Add(i);
                Deck.Add(i);
                Deck.Add(i);
                Deck.Add(i);
            }

        }

         
        public void GameStart()
        {
            //人数確認
            if (playerList.Count != 2) return;

            foreach(Player player in playerList)
            {
                Distribute(player, 7);
                SendData(player, SendParameter.Hand, player.GetHand());
                SendData(player, SendParameter.OpponentName, GetOtherPlayers(player)[0].Name);
            }

            SendData(playerList[0], SendParameter.Turn, "0");
            playerList[0].Turn = true;
            return;

        }
        
        Random random = new Random();
        /// <summary>
        /// カードを配る
        /// </summary>
        /// <param name="player"></param>
        /// <param name="CardCount">枚数</param>
        void Distribute(Player player, int CardCount)
        {
            for (int i = 0; i < CardCount; i++)
            {
                int index = random.Next(0, Deck.Count - 1);
                player.Hand.Add(Deck[index]);
                Deck.RemoveAt(index);
            }
        }


        Player GetPlayer(Socket socket)
        {
            
            foreach(Player player in playerList)
            {
                if (player.socket == socket) return player;
            }

            return null;
        }
        List<Player> GetOtherPlayers(Player player)
        {
            List<Player> _playerlList = new List<Player>(playerList);
            _playerlList.Remove(player);
            return _playerlList;
        }

        void SendDataAllPlayer(SendParameter Parameter, string Content)
        {
            foreach(Player player in playerList)
            {
                SendData(player, Parameter, Content);
            }
        }
        void SendData(Socket clientSocket, SendParameter Parameter, string Content)
        {
            string data = Parameter.ToString("G") + "=" + Content;
            base.Send(clientSocket, data);
        }
        void SendData(Player player, SendParameter Parameter, string Content)
        {
            SendData(player.socket, Parameter, Content);
        }
        enum SendParameter
        {
            OpponentName,
            Hand,
            OpponentHand,
            Turn,
            Error,
            Finish,
            Start,
            DrawPile
        }

        protected override void OnConnectNewClient(StateObject stateObject)
        {
            
            if (playerList.Count > 2)
            {
                SendData(stateObject.ClientSocket, SendParameter.Error, "OverCapacity");

            }
            else
            {
                playerList.Add(new Player(stateObject.ClientSocket));
            }

            if(playerList.Count == 2)
            {
                GameStart();
            }
            

        }

        protected override void OnDisconnect(StateObject stateObject)
        {
            playerList.Remove(GetPlayer(stateObject.ClientSocket));
            SendDataAllPlayer(SendParameter.Error, "OpponentDisconnect");
            Console.WriteLine("Disconnect");
        }

        protected override void OnReceive(StateObject stateObject, string Msg)
        {
            Player player = GetPlayer(stateObject.ClientSocket);
            Player opponent = default;
            
            if (GetOtherPlayers(player).Count != 0)
            {
                opponent = GetOtherPlayers(player)[0];
            }
            

            string[] MsgSplit = Msg.Split('=');
            string Parameter = MsgSplit[0];
            string Content = MsgSplit[1];

            switch (Parameter)
            {
                case "PlayerName":
                    if (!player.SetName(Content)) SendData(player, SendParameter.Error, "AlreadySetPlayerName");
                    break;
                case "Select":
                    GameProsess(player, Content);
                    break;
                case "Draw":
                    if (player.Turn)
                    {
                        Distribute(player, int.Parse(Content));
                    }
                    Distribute(player, int.Parse(Content));
                    SendData(player, SendParameter.Hand, player.GetHand());
                    break;
                case "Uno":
                    if (player.Hand.Count == 1)
                    {
                        player.UnoFrag = true;
                    }
                    else
                    {
                        SendData(player, SendParameter.Error, "UnExpectedError");
                    }

                    break;
                case "DataRequest":
                    switch (Content)
                    {
                        case "OpponentName":
                            SendData(player, SendParameter.OpponentName, opponent.Name);
                            break;
                        case "Hand":
                            SendData(player, SendParameter.Hand, player.GetHand());
                            break;
                        case "OpponentHand":
                            SendData(player, SendParameter.OpponentHand, opponent.GetOpenedHand());
                            break;
                        default:
                            SendData(player, SendParameter.Error, "UnexpectedError");
                            break;
                    }
                    break;
                default:
                    SendData(player, SendParameter.Error, "UnexpectedError");
                    break;
            }
        }

        /// <summary>
        /// Selectで送られてきたデータの処理と次のプレイヤーへの受け渡し。
        /// </summary>
        /// <param name="player"></param>
        /// <param name="Content">CardIDの,区切り</param>
        void GameProsess(Player player, string Content)
        {
            player.Turn = false;

            string[] CardIDArray = Content.Split(',');
            //Playerの手札と照合
            List<int> PlayerHand = player.Hand;
            bool NotExist = false;
            foreach(string CardID in CardIDArray)
            {
                if (!PlayerHand.Exists(x => x == int.Parse(CardID)))
                {
                    NotExist = true;
                    break;
                }
                PlayerHand.Remove(int.Parse(CardID));
            }
            //選択したカードに手札以外が含まれている。
            if (NotExist)
            {
                SendData(player, SendParameter.Error, "CardDoesntExist");
                SendData(player, SendParameter.Hand, player.GetHand());
                player.Turn = true;
                SendData(player, SendParameter.Turn, "0");
            }
            if (IsAppropriate(CardIDArray))
            {
                SendDataAllPlayer(SendParameter.DrawPile, Content);
                TopDrawCard = int.Parse(CardIDArray[CardIDArray.Length - 1]);
                player.Hand = PlayerHand;
                SendData(player, SendParameter.Hand, player.GetHand());
                if(player.Hand.Count == 0)
                {

                        SendData(player, SendParameter.Finish, "Win");
                        SendData(GetOtherPlayers(player)[0], SendParameter.Finish, "Lose");
                        Console.WriteLine("We Finish the Game.");
                 }
                else
                {
                    GetOtherPlayers(player)[0].Turn = true;
                    SendData(GetOtherPlayers(player)[0], SendParameter.Turn, "0");
                }
                
            }
            else
            {
                SendData(player, SendParameter.Hand, player.GetHand());
                GetOtherPlayers(player)[0].Turn = true;
                SendData(player, SendParameter.Turn, "0");
            }

        }

        bool IsAppropriate(string[] StrCardIDArray)
        {
            List<int> CardIDArray = new List<int>();

            foreach(string strCardID in StrCardIDArray)
            {
                CardIDArray.Add(int.Parse(strCardID));
            }

            //未選択カードを弾く
            foreach(int CardID in CardIDArray)
            {
                if (CardID >= 400) return false;
            }


            //すべてChangeカードならば、真を返す。
            bool ChangeDFlag = true;
            bool ChangeFlag = true;
            foreach (int CardID in CardIDArray)
            {
                if (CardID % 100 != 12) ChangeDFlag = false;
                if (CardID % 100 != 13) ChangeFlag = false;

            }
            if (ChangeDFlag || ChangeFlag) return true;


            //場のカードと色、数字が違うようなカードが1枚めに来たとき
            if (TopDrawCard % 100 != CardIDArray[0] % 100 && TopDrawCard / 100 != CardIDArray[0] / 100) return false;
            





            //すべてのカードが同じ数字か確認
            for(int i = 0; i> CardIDArray.Count; i++)
            {
                if (CardIDArray[i] % 100 != CardIDArray[i + 1] % 100) return false;
            }



            ///ルール通り何かつける。
            return true;
        }


    }

    public class Player
    {
        public Socket socket { get; private set; }
        public string Name { get; private set; } = "";
        public List<int> Hand = new List<int>();
        public List<int> OpenedHand = new List<int>();
        public bool UnoFrag = false;
        public bool Turn = false;

        public Player(Socket _socket)
        {
            socket = _socket;
        }

        public bool SetName(string _Name)
        {
            if(Name == "")
            {
                Name = _Name;
                return true;
            }
            else
            {
                return false;
            }
        }

        public string GetHand()
        {
            return string.Join(",", Hand);
        }
        public string GetOpenedHand()
        {
            return string.Join(",", OpenedHand);
        }
    }
}
