using AkiraMindController.Communication;
using AkiraMindController.Communication.AkariCommand;
using AkiraMindController.Communication.Bases;
using AkiraMindController.Communication.Connectors.CommonMessages;
using AkiraMindController.Communication.Connectors.ConnectorImpls.Http;
using AkiraMindController.Communication.Utils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using static AkiraMindController.Communication.AkariCommand.GetNoteManagerValue;
using static AkiraMindController.Communication.Connectors.CommonMessages.Ping;

namespace MindControllerTestGUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public bool IsConnected
        {
            get => isConnected;
            set
            {
                if (isConnected != value && !value)
                    AppendOutputLine("Disconnected.");
                isConnected = value;
                PropertyChanged?.Invoke(this, new(nameof(IsConnected)));
            }
        }

        public string Output
        {
            get { return (string)GetValue(OutputProperty); }
            set { SetValue(OutputProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Output.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty OutputProperty =
            DependencyProperty.Register("Output", typeof(string), typeof(MainWindow), new PropertyMetadata(""));

        public int Port
        {
            get { return (int)GetValue(PortProperty); }
            set { SetValue(PortProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Port.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty PortProperty =
            DependencyProperty.Register("Port", typeof(int), typeof(MainWindow), new PropertyMetadata(30000));

        public int SeekTime
        {
            get { return (int)GetValue(SeekTimeProperty); }
            set { SetValue(SeekTimeProperty, value); }
        }

        // Using a DependencyProperty as the backing store for SeekTime.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty SeekTimeProperty =
            DependencyProperty.Register("SeekTime", typeof(int), typeof(MainWindow), new PropertyMetadata(0));

        public bool IsPlayAfterSeek
        {
            get { return (bool)GetValue(IsPlayAfterSeekProperty); }
            set { SetValue(IsPlayAfterSeekProperty, value); }
        }

        // Using a DependencyProperty as the backing store for IsPlayAfterSeek.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsPlayAfterSeekProperty =
            DependencyProperty.Register("IsPlayAfterSeek", typeof(bool), typeof(MainWindow), new PropertyMetadata(false));

        public float FieldLeftX
        {
            get { return (float)GetValue(FieldLeftXProperty); }
            set { SetValue(FieldLeftXProperty, value); }
        }

        // Using a DependencyProperty as the backing store for FieldLeftX.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty FieldLeftXProperty =
            DependencyProperty.Register("FieldLeftX", typeof(float), typeof(MainWindow), new PropertyMetadata(0f));

        public float FieldRightX
        {
            get { return (float)GetValue(FieldRightXProperty); }
            set { SetValue(FieldRightXProperty, value); }
        }

        // Using a DependencyProperty as the backing store for FieldRightX.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty FieldRightXProperty =
            DependencyProperty.Register("FieldRightX", typeof(float), typeof(MainWindow), new PropertyMetadata(0f));

        public float PlayerX
        {
            get { return (float)GetValue(PlayerXProperty); }
            set { SetValue(PlayerXProperty, value); }
        }

        // Using a DependencyProperty as the backing store for PlayerX.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty PlayerXProperty =
            DependencyProperty.Register("PlayerX", typeof(float), typeof(MainWindow), new PropertyMetadata(0f));

        public float FieldCenterX
        {
            get { return (float)GetValue(FieldCenterXProperty); }
            set { SetValue(FieldCenterXProperty, value); }
        }

        // Using a DependencyProperty as the backing store for FieldCenterX.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty FieldCenterXProperty =
            DependencyProperty.Register("FieldCenterX", typeof(float), typeof(MainWindow), new PropertyMetadata(0f));

        public float JudeDraw
        {
            get { return (float)GetValue(JudeDrawProperty); }
            set { SetValue(JudeDrawProperty, value); }
        }

        // Using a DependencyProperty as the backing store for JudeDraw.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty JudeDrawProperty =
            DependencyProperty.Register("JudeDraw", typeof(float), typeof(MainWindow), new PropertyMetadata(0f));

        public bool IsEnableAutoPlay
        {
            get { return (bool)GetValue(IsEnableAutoPlayProperty); }
            set { SetValue(IsEnableAutoPlayProperty, value); }
        }

        // Using a DependencyProperty as the backing store for IsEnableAutoPlay.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsEnableAutoPlayProperty =
            DependencyProperty.Register("IsEnableAutoPlay", typeof(bool), typeof(MainWindow), new PropertyMetadata(false, (e, s) => ((MainWindow)e).OnIsEnableAutoPlayChanged()));



        public float FakeButtomSec
        {
            get { return (float)GetValue(FakeButtomSecProperty); }
            set { SetValue(FakeButtomSecProperty, value); }
        }

        // Using a DependencyProperty as the backing store for FakeButtomSec.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty FakeButtomSecProperty =
            DependencyProperty.Register("FakeButtomSec", typeof(float), typeof(MainWindow), new PropertyMetadata(500f));



        private HttpConnectorClient client;
        private bool isConnected;
        private DispatcherTimer timer;

        public event PropertyChangedEventHandler PropertyChanged;

        public MainWindow()
        {
            InitializeComponent();
            SimpleInterfaceImplement.Deserialize = (json, type) => JsonConvert.DeserializeObject(json, type);
            SimpleInterfaceImplement.Serialize = (obj) => JsonConvert.SerializeObject(obj);
            SimpleInterfaceImplement.Log = x => Debug.WriteLine(x);

            timer = new DispatcherTimer();
            var ms = 500;
            var pingMs = 10000;
            timer.Interval = TimeSpan.FromMilliseconds(ms);
            var i = 0;
            timer.Tick += (a, b) =>
            {
                if (i++ > pingMs / ms)
                {
                    IsConnected = client?.SendMessageWithResponse<Ping, Pong>() is Pong;
                    i = 0;
                }

                if (IsConnected && client?.SendMessageWithResponse<GetNoteManagerValue, ReturnValue>() is ReturnValue data)
                {
                    FieldLeftX = data.posInL;
                    FieldRightX = data.posInR;
                    FieldCenterX = data.posInC;
                    PlayerX = data.playerPosX;
                }
            };

            timer.Start();

            double CalculateXFromTwoPointFormFormula(double y, double x1, double y1, double x2, double y2)
            {
                double num = y2 - y1;
                double num2 = x2 - x1;
                bool flag = num == 0.0;
                double result;
                if (flag)
                {
                    result = x1;
                }
                else
                {
                    result = (y - y1) * 1.0 / num * num2 + x1;
                }
                return result;
            }
        }

        private void OnIsEnableAutoPlayChanged()
        {
            client.SendMessage(new AutoPlay()
            {
                isEnable = IsEnableAutoPlay
            });
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            //pause
            client.SendMessage<PauseGamePlay>();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            //connect 
            client = new HttpConnectorClient(Port);
            IsConnected = client?.SendMessageWithResponse<Ping, Pong>() is Pong;
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            //resume
            client.SendMessage<ResumeGamePlay>();
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            //restart
            client.SendMessage<RestartGamePlay>();
        }

        private void Button_Click_4(object sender, RoutedEventArgs e)
        {
            //getData
            var result = client.SendMessageWithResponse<GetNoteManagerValue, ReturnValue>();
            AppendOutputLine(JsonConvert.SerializeObject(result, Formatting.Indented));
        }

        private void Button_Click_5(object sender, RoutedEventArgs e)
        {
            //seek
            client.SendMessage(new SeekToGamePlay() { audioTimeMsec = SeekTime, playAfterSeek = IsPlayAfterSeek });
        }

        private async void AppendOutputLine(string content)
        {
            var isScrollToEnd = scrollViewer.VerticalOffset == scrollViewer.ContentVerticalOffset;
            Output += content;
            Output += Environment.NewLine;

            if (isScrollToEnd)
            {
                await Dispatcher.Yield();
                scrollViewer.ScrollToEnd();
            }
        }

        private void Button_Click_6(object sender, RoutedEventArgs e)
        {
            //clear
            Output = "";
        }

        private void Button_Click_7(object sender, RoutedEventArgs e)
        {
            client.SendMessage(new SetNoteManagerValue() { name = "fakeButtomMsec", value = FakeButtomSec.ToString() });
        }

        private void Button_Click_8(object sender, RoutedEventArgs e)
        {
            if (client.SendMessageWithResponse<GetNoteManagerAutoPlayData, GetNoteManagerAutoPlayData.ReturnValue>() is not GetNoteManagerAutoPlayData.ReturnValue data)
                return;

            void print(AutoFaderTarget r)
            {
                AppendOutputLine($"finalTargetPlace : {r.targetPlaceRange}");
                AppendOutputLine($"finalTargetFrame : {r.finalTargetFrame}");

                AppendOutputLine($"moveableRange : {r.moveableRange}");
                AppendOutputLine($"damageRanges : {string.Join(" ", r.damageRanges.Select(x => x.ToString()).ToArray())}");
                AppendOutputLine($"bellRanges : {string.Join(" ", r.bellRanges.Select(x => x.ToString()).ToArray())}");
                AppendOutputLine($"targetRanges : {string.Join(" ", r.targetRanges.Select(x => x.ToString()).ToArray())}");
            }

            AppendOutputLine($"------------GetNoteManagerAutoPlayData.ReturnValue dumper-----------");
            AppendOutputLine($"autoPlay: {data.autoPlay}");
            AppendOutputLine($"autoFader: {data.autoFader}");
            AppendOutputLine($"------------curFaderTarget-----------");
            var curFaderTarget = new AutoFaderTarget();
            curFaderTarget.Deerialize(data.curFaderTargetStr);
            print(curFaderTarget);
            AppendOutputLine($"------------prevFaderTarget-----------");
            var prevFaderTarget = new AutoFaderTarget();
            prevFaderTarget.Deerialize(data.prevFaderTargetStr);
            print(prevFaderTarget);
            AppendOutputLine($"--------------------------------------------");

        }

		private void Button_Click_9(object sender, RoutedEventArgs e)
		{
			client.SendMessage(new PlayNoteSE());
		}
	}
}
