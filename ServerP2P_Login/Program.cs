﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServerP2P_Login
{
    /// <summary>
    /// Login 서버 샘플
    /// </summary>
    public class LoginServer
    {
        public ZNet.CoreServerNet m_Core = null;

        public Rmi.Proxy proxy;
        public Rmi.Stub stub;

        public LoginServer()
        {
            m_Core = new ZNet.CoreServerNet();

            proxy = new Rmi.Proxy();
            stub = new Rmi.Stub();

            m_Core.Attach(proxy, stub);

            // 메세지 패킷 처리 샘플
            stub.request_message = (ZNet.RemoteID remote, ZNet.CPackOption pkOption, RemoteClass.CUserClass testClass, Dictionary<Int32, Int32> dic_test, string msg) =>
            {
                Console.WriteLine("Remote[{0}] msg : {1}", remote, msg);
                proxy.reponse_message(remote, ZNet.CPackOption.Basic, testClass, dic_test, msg);
                return true;
            };

            // 서버이동 요청 패킷 처리 : 요청 받은 서버타입중 원활한 서버를 찾아서 수동으로 접속할 수 있게 주소를 보내준다
            stub.request_move_to_server = (ZNet.RemoteID remote, ZNet.CPackOption pkOption, int server_type) =>
            {
                ZNet.MasterInfo selectSvr;
                if (m_Core.SelectServer(server_type, out selectSvr))
                {
                    proxy.reponse_move_to_server(remote, ZNet.CPackOption.Basic, true, selectSvr.m_Addr);
                }
                else
                {
                    proxy.reponse_move_to_server(remote, ZNet.CPackOption.Basic, false, new ZNet.NetAddress());
                }
                return true;
            };

            // 클라이언트가 이 서버에 입장된 시점
            m_Core.client_join_handler = (ZNet.RemoteID remote, ZNet.NetAddress addr) =>
            {
                Console.WriteLine("Client {0} is Join {1}:{2}.\n", remote, addr.m_ip, addr.m_port);
            };

            // 클라이언트가 이 서버에 퇴장하는 시점
            m_Core.client_leave_handler = (ZNet.RemoteID remote) =>
            {
                Console.WriteLine("Client {0} Leave.\n", remote);
            };


            // 서버 이동 시작 시점 : 완료 이벤트는 이동 성공한 서버에서 발생
            m_Core.move_server_start_handler = (ZNet.RemoteID remote, out ZNet.ArrByte buffer) =>
            {
                // 여기서는 이동할 서버로 동기화 시킬 유저 데이터를 구성하여 buffer에 넣어둔다 -> 완료서버에서 해당 데이터를 그대로 받게된다
                ZNet.CMessage msg = new ZNet.CMessage();
                ServerP2P_Common.UserDataSync user_data;

                user_data.info = "유저 데이터 정보, DBID=1234, 로그인서버입니다";
                user_data.item_id = 12312309871234;

                msg.Write(user_data.info);
                msg.Write(user_data.item_id);

                buffer = msg.m_array;

                Console.WriteLine("move server start  {0}  {1}", user_data.info, user_data.item_id);
            };

            // 서버 이동 완료 시점
            m_Core.move_server_complete_handler = (ZNet.RemoteID remote, ZNet.ArrByte buffer) =>
            {
                // 이동 시작한 서버에서 구성해둔 유저 데이터 버퍼를 이용해 동기화 처리한다
                ZNet.CMessage msg = new ZNet.CMessage();
                msg.m_array = buffer;

                ServerP2P_Common.UserDataSync user_data;
                msg.Read(out user_data.info);
                msg.Read(out user_data.item_id);
                Console.WriteLine("move server complete  {0}  {1}", user_data.info, user_data.item_id);
            };


            m_Core.message_handler = (ZNet.ResultInfo result) =>
            {
                string str_msg = "Msg : ";
                str_msg += result.msg;
                Console.WriteLine(str_msg);
            };
            m_Core.exception_handler = (Exception e) =>
            {
                string str_msg = "Exception : ";
                str_msg += e.ToString();
                Console.WriteLine(str_msg);
            };


            // server p2p관련 이벤트
            m_Core.server_join_hanlder = (ZNet.RemoteID remote, ZNet.NetAddress addr) =>
            {
                Console.WriteLine(string.Format("서버P2P맴버 입장 remoteID {0}", remote));
            };

            m_Core.server_leave_hanlder = (ZNet.RemoteID remote, ZNet.NetAddress addr) =>
            {
                Console.WriteLine(string.Format("서버P2P맴버 퇴장 remoteID {0}", remote));
            };

            m_Core.server_master_join_hanlder = (ZNet.RemoteID remote) =>
            {
                Console.WriteLine(string.Format("마스터서버에 입장성공 remoteID {0}", remote));
            };

            m_Core.server_master_leave_hanlder = () =>
            {
                Console.WriteLine(string.Format("마스터서버와 연결종료!!!"));
            };

            m_Core.server_refresh_hanlder = (ZNet.MasterInfo master_info) =>
            {
                Console.WriteLine(string.Format("서버P2P remote:{0} type:{1}[{2}] current:{3} addr:{4}:{5}",
                    master_info.m_remote,
                    (ServerP2P_Common.Server)master_info.m_ServerType,
                    master_info.m_Description,
                    master_info.m_Clients,
                    master_info.m_Addr.m_ip,
                    master_info.m_Addr.m_port
                    ));
            };
        }
    }


    class Program
    {
        static void Main(string[] args)
        {
            LoginServer Svr = new LoginServer();

            ZNet.StartOption param = new ZNet.StartOption();
            param.m_IpAddressListen = ServerP2P_Common.Join.ipaddr;
            param.m_PortListen = ServerP2P_Common.Join.portnum;
            param.m_MaxConnectionCount = 2000;
            param.m_RefreshServerTickMs = 10000;
            param.m_ProtocolVersion = ServerP2P_Common.Join.protocol_ver;

            Svr.m_Core.SetKeepAliveOption(30);

            ZNet.ResultInfo outResult = new ZNet.ResultInfo();
            if (Svr.m_Core.Start(param, outResult))
            {
                Console.WriteLine("LoginServer Start ok.\n");
                DisplayHelpCommand();
            }
            else
            {
                Console.WriteLine("Start error : {0} \n", outResult.msg);
            }

            // master client connect
            Svr.m_Core.MasterConnect(
                ServerP2P_Common.MasterServerConnect.master_ipaddr,
                ServerP2P_Common.MasterServerConnect.master_portnum,
                "LoginServer",
                (int)ServerP2P_Common.Server.Login
                );

            var ret = ReadLineAsync();
            bool run_program = true;
            while (run_program)
            {
                if (ret.IsCompleted)
                {
                    switch (ret.Result)
                    {
                        case "/h":
                            DisplayHelpCommand();
                            break;

                        case "/stat":
                            DisplayStatus(Svr.m_Core);
                            break;

                        case "/s":  // 현시점에서 가장 원활한 Main 서버 정보를 출력해보기 : 서버이동시 선택될 확률이 가장 높은 서버
                            ZNet.MasterInfo selectSvr;
                            if (Svr.m_Core.SelectServer((int)ServerP2P_Common.Server.Main, out selectSvr))
                            {
                                Console.WriteLine("select svr {0} {1}", (ServerP2P_Common.Server)selectSvr.m_ServerType, selectSvr.m_Addr.m_port);
                            }
                            else
                            {
                                Console.WriteLine("select svr empty");
                            }
                            break;

                        case "/q":
                            Console.WriteLine("quit Server...");
                            run_program = false;
                            break;
                    }

                    if (run_program)
                        ret = ReadLineAsync();
                }

                System.Threading.Thread.Sleep(10);
            }

            Console.WriteLine("Start Closing...  ");
            Svr.m_Core.Dispose();
            Console.WriteLine("Close complete.");

            System.Threading.Thread.Sleep(1000 * 2);
        }

        static async Task<string> ReadLineAsync()
        {
            var line = await Task.Run(() => Console.ReadLine());
            return line;
        }

        static void DisplayHelpCommand()
        {
            Console.WriteLine("/Cmd:  q(Quit) h(Help) stat(status info)");
        }

        static void DisplayStatus(ZNet.CoreServerNet svr)
        {
            ZNet.ServerState status;
            svr.GetCurrentState(out status);


            // 기본 정보
            Console.WriteLine(string.Format(
                "[NetInfo]  Connect/Join {0}({1})/{2}  Connect(Server) {3}/{4}  Accpet/Max {5}/{6}",

                // 실제 연결된 client
                status.m_CurrentClient,

                // 연결복구 처리과정인 client
                status.m_RecoveryCount,

                // 서버에 입장완료상태의 client
                status.m_JoinedClient,

                // 서버간 direct p2p 연결된 server
                status.m_ServerP2PCount,

                // 서버간 direct p2p 연결 모니터링중인 server(서버간 연결 자동복구를 위한 모니터링)
                status.m_ServerP2PConCount,

                // 이 서버에 추가 연결 가능한 숫자
                status.m_nIoAccept,

                // 이 서버에 최대 연결 가능한 숫자
                status.m_MaxAccept
                ));


            // 엔진 내부에서 작업중인 IO 관련 상태 정보
            Console.WriteLine(string.Format(
                "[IO Info]  Close {0}  Event {1}  Recv {2}  Send {3}",

                // current io close
                status.m_nIoClose,

                // current io event
                status.m_nIoEvent,

                // current io recv socket
                status.m_nIoRecv,

                // current io send socket
                status.m_nIoSend
            ));


            // 엔진 메모리 관련 사용 정보
            Console.WriteLine(string.Format(
                "[MemInfo]  Alloc/Instant[{0}/{1}], test[{2}], EngineVersion[{3}.{4:0000}] ",

                // 미리 할당된 IO 메모리
                status.m_nAlloc,

                // 즉석 할당된 IO 메모리
                status.m_nAllocInstant,

                // test data
                status.m_test_data,

                // Core버전
                svr.GetCoreVersion() / 10000,
                svr.GetCoreVersion() % 10000
            ));


            // 스레드 정보
            string strThr = "[ThreadInfo] (";
            int MaxDisplayThreadCount = status.m_arrThread.Count();
            if (MaxDisplayThreadCount > 8)   // 화면이 복잡하니까 그냥 최대 8개까지만 표시
            {
                strThr += MaxDisplayThreadCount;
                strThr += ") : ";
                MaxDisplayThreadCount = 8;
            }
            else
            {
                strThr += MaxDisplayThreadCount;
                strThr += ") : ";
            }

            for (int i = 0; i < MaxDisplayThreadCount; i++)
            {
                strThr += "[";
                strThr += status.m_arrThread[i].m_ThreadID;     // 스레드ID
                strThr += "/";
                strThr += status.m_arrThread[i].m_CountQueue;   // 처리 대기중인 작업
                strThr += "/";
                strThr += status.m_arrThread[i].m_CountWorked;  // 처리된 작업(누적)
                strThr += "] ";
            }
            Console.WriteLine(strThr);
        }
    }
}
