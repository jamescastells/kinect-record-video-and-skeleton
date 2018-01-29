using Microsoft.Kinect;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
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
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;
using System.ComponentModel;
using System.Windows.Automation.Peers;

namespace KinectStreams
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Members

        Mode _mode = Mode.Color;

        KinectSensor _sensor;
        MultiSourceFrameReader _reader;
        IList<Body> _bodies;
        bool _recording = false;

        bool _displayBody = true;

        System.Timers.Timer aTimer = new System.Timers.Timer();
        System.Timers.Timer bTimer = new System.Timers.Timer();
        int segundos = 0;
        int frame_number = 0;
        int frame_ps = 0;


        //MQTT
        MqttClient mqtt_client = new MqttClient("200.126.23.131");
        
        #endregion

        #region Constructor

        public MainWindow()
        {
            InitializeComponent();
            aTimer.Elapsed += new ElapsedEventHandler(OnTimedEvent);
            aTimer.Interval = 1000;
            aTimer.Enabled = false;
            bTimer.Elapsed += new ElapsedEventHandler(OnTimedEvent2);
            bTimer.Interval = 1000;
            bTimer.Enabled = true;

            byte code = mqtt_client.Connect(Guid.NewGuid().ToString(), "james", "james");
            mqtt_client.MqttMsgPublished += client_MqttMsgPublished;

            
            mqtt_client.MqttMsgPublishReceived += client_MqttMsgPublishReceived;
            mqtt_client.Subscribe(new string[] { "/rap_start", "/rap_stop", "/rap_name", "/rap_status" },
                            new byte[] { MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE,
                                         MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE,
                                         MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE,
                                         MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE});

        }

        void client_MqttMsgPublished(object sender, MqttMsgPublishedEventArgs e)
        {
            Debug.WriteLine("Message id = " + e.MessageId + " Published = " + e.IsPublished);
        }

        void client_MqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs e)
        {
            Debug.WriteLine("Received = " + Encoding.UTF8.GetString(e.Message) + " on topic " + e.Topic);
            if (e.Topic == "/rap_name")
            {
                this.textBox.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal,
                    new Action(delegate ()
                    {
                        textBox.Text = Encoding.UTF8.GetString(e.Message);
                    }));                
            }else if (e.Topic == "/rap_start" && !_recording)
            {
                this.recordbutton.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal,
                    new Action(delegate ()
                    {
                        this.audience.IsChecked = false;
                        textBox.Text = Encoding.UTF8.GetString(e.Message).Split('|')[0];
                        recordbutton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    }));
                
            }else if (e.Topic == "/rap_stop" && _recording)
            {
                this.stopbutton.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal,
                   new Action(delegate ()
                   {
                       stopbutton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                   }));
            }else if (e.Topic == "/rap_status")
            {
                ushort msgId = mqtt_client.Publish("/rap_ack_status",
                    Encoding.UTF8.GetBytes("0|date|"+ get_status()),
                    MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE,
                    false);
            }

        }

        private string get_status()
        {
            if (_recording)
                return "2";
            else
                return "1";
        }
        private void OnTimedEvent(object source, ElapsedEventArgs e)
        {
            this.Dispatcher.Invoke(() =>
            {
                segundos++;
                int minuto = segundos / 60;
                int segundo = segundos % 60;
                if (segundo>=10)
                    this.time_label.Content = minuto.ToString() + ":" + segundo.ToString();
                else
                    this.time_label.Content = minuto.ToString() + ":0" + segundo.ToString();
            });
        }
        private void OnTimedEvent2(object source, ElapsedEventArgs e)
        {
             this.Dispatcher.Invoke(() =>
            {
                fpslabel.Content = frame_ps + " FPS";
                frame_ps = 0;
            });
        }
        #endregion

        #region Event handlers

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _sensor = KinectSensor.GetDefault();
           

            if (_sensor != null)
            {
                _sensor.Open();

                _reader = _sensor.OpenMultiSourceFrameReader(FrameSourceTypes.Color | FrameSourceTypes.Depth | FrameSourceTypes.Infrared | FrameSourceTypes.Body);
                _reader.MultiSourceFrameArrived += Reader_MultiSourceFrameArrived;
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            if (_reader != null)
            {
                _reader.Dispose();
            }

            if (_sensor != null)
            {
                _sensor.Close();
            }
        }

        void Reader_MultiSourceFrameArrived(object sender, MultiSourceFrameArrivedEventArgs e)
        {
            var reference = e.FrameReference.AcquireFrame();
            // Body
            var frame_b = reference.BodyFrameReference.AcquireFrame();
            // using (var frame = reference.BodyFrameReference.AcquireFrame())
            //    {

            if (frame_b != null)
            {
                canvas.Children.Clear();

                _bodies = new Body[frame_b.BodyFrameSource.BodyCount];

                frame_b.GetAndRefreshBodyData(_bodies);

                foreach (var body in _bodies)
                {
                    if (body != null)
                    {
                        if (body.IsTracked)
                        {
                            // Draw skeleton.
                            if (_displayBody)
                            {
                                canvas.DrawSkeleton(body, _recording, this.textBox.Text, _sensor, labelposture, segundos, frame_number);
                            }
                        }
                    }

                }
                //   }
                frame_b.Dispose();
                // Color
            var frame = reference.ColorFrameReference.AcquireFrame();
           
            //using (var frame = reference.ColorFrameReference.AcquireFrame())
            //{

            if (frame != null)
                {
                double fps = 1.0 / frame.ColorCameraSettings.FrameInterval.TotalSeconds;
                Console.Write(fps);
                Console.Write("\n");
                if (_mode == Mode.Color)
                    {
                        camera.Source = frame.ToBitmap(_recording, frame_number, this.textBox.Text);
                        frame_ps++;
                        if (_recording)
                            frame_number++; 
                    }
                    if (!_recording)
                        frame.Dispose();
                }
                
            //}

            // Depth
            /* using (var frame = reference.DepthFrameReference.AcquireFrame())
             {
                 if (frame != null)
                 {
                     if (_mode == Mode.Depth)
                     {
                         camera.Source = frame.ToBitmap();
                     }
                 }
             }

             // Infrared
             using (var frame = reference.InfraredFrameReference.AcquireFrame())
             {
                 if (frame != null)
                 {
                     if (_mode == Mode.Infrared)
                     {
                         camera.Source = frame.ToBitmap();
                     }
                 }
             }*/
            }
            
        }

        private void Color_Click(object sender, RoutedEventArgs e)
        {
            _mode = Mode.Color;
        }

        private void Depth_Click(object sender, RoutedEventArgs e)
        {
            _mode = Mode.Depth;
        }

        private void Infrared_Click(object sender, RoutedEventArgs e)
        {
            _mode = Mode.Infrared;
        }

        private void Body_Click(object sender, RoutedEventArgs e)
        {
            _displayBody = !_displayBody;
        }

        #endregion

        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Record_Click(object sender, RoutedEventArgs e)
        {
            if (this.textBox.Text == "Name of the project")
            {
                MessageBox.Show("Please insert a name of file.", "Saved", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            this.textBox.Text.Replace(" ", "");
            _recording = true;

            this.textBox.IsEnabled = false;
            this.recordbutton.Content = "Recording...";
            this.recordbutton.IsEnabled = false;
            this.stopbutton.IsEnabled = true;
            File.WriteAllText("results/"+this.textBox.Text+".csv", "");
            System.IO.Directory.CreateDirectory("results/" + this.textBox.Text);
            aTimer.Enabled = true;
            if (this.audience.IsChecked == true)
            {
                Process proc = null;
                try
                {
                    string batDir = string.Format(@"");
                    proc = new Process();
                    proc.StartInfo.WorkingDirectory = batDir;
                    proc.StartInfo.FileName = "open_audience.bat";
                    proc.StartInfo.CreateNoWindow = false;
                    proc.Start();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.StackTrace.ToString());
                }
            }
        }

        private void textBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (this.textBox.Text == "Name of the project")
                this.textBox.Text = "";
        }

        private void stopbutton_Click(object sender, RoutedEventArgs e)
        {
            _recording = false;
            string name_of_file = this.textBox.Text;
            int fps = frame_number / segundos;
            /*Process proc = null;
            try
            {
                string batDir = string.Format(@"");
                proc = new Process();
                proc.StartInfo.WorkingDirectory = batDir;
                proc.StartInfo.FileName = "make_video.bat";
                proc.StartInfo.Arguments = name_of_file + " " + fps; 
                proc.StartInfo.CreateNoWindow = false;
                proc.Start();
                //proc.WaitForExit();                
                MessageBox.Show("Saved file.","Saved",MessageBoxButton.OK,MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.StackTrace.ToString());
            }*/
            this.textBox.IsEnabled = true;
            this.recordbutton.Content = "Record";
            this.recordbutton.IsEnabled = true;
            this.stopbutton.IsEnabled = false;
            this.textBox.Text = "Name of the project";
            this.time_label.Content = "0:00";
            this.labelposture.Content = "No body";
            aTimer.Enabled = false;
            segundos = 0;
            frame_number = 0;
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            mqtt_client.Disconnect();

        }
    }

    public enum Mode
    {
        Color,
        Depth,
        Infrared
    }
}
