using Microsoft.Kinect;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace KinectStreams
{
    public static class Extensions
    {
        #region Camera

        static int width, height, stride;
        static PixelFormat format = PixelFormats.Bgr32;
        static byte[] pixels;

        static bool must_record = false;

        //private static int contador = 0;

        public static ImageSource ToBitmap(this ColorFrame frame, bool recording, int frame_number, String name)
        {

            width = frame.FrameDescription.Width;
            height = frame.FrameDescription.Height;
            //PixelFormat format = PixelFormats.Bgr32;

            pixels = new byte[width * height * ((format.BitsPerPixel + 7) / 8)];

            if (frame.RawColorImageFormat == ColorImageFormat.Bgra)
            {
                frame.CopyRawFrameDataToArray(pixels);
            }
            else
            {
                frame.CopyConvertedFrameDataToArray(pixels, ColorImageFormat.Bgra);
            }

            stride = width * format.BitsPerPixel / 8;
            if (must_record == true)
            {
                ThreadPool.QueueUserWorkItem(writeColorImage, new object[] { frame, frame_number, name });
                must_record = false;
            }
            else
                frame.Dispose();
            
            
            return BitmapSource.Create(width, height, 96, 96, format, null, pixels, stride);
        }

        private static void writeColorImage(object objetos)
        {
            object[] array = objetos as object[];
            object f = array[0];
            object frame_number =array[1];
            object name = array[2];
            var frame = (ColorFrame)f;
            int width = frame.FrameDescription.Width;
            int height = frame.FrameDescription.Height;
            BitmapEncoder encoder = new PngBitmapEncoder();
            WriteableBitmap colorBitmap = new WriteableBitmap(width, height, 96.0, 96.0, PixelFormats.Bgr32, null);

            frame.CopyConvertedFrameDataToIntPtr(colorBitmap.BackBuffer, (uint)(width * height * 4), ColorImageFormat.Bgra);
            frame.Dispose();
            encoder.Frames.Add(BitmapFrame.Create(colorBitmap));
            String st;
            int contador = (int)frame_number + 1;
            if (contador < 10)
                st = "0000" + contador.ToString();
            else if (contador < 100)
                st = "000" + contador.ToString();
            else if (contador < 1000)
                st = "00" + contador.ToString();
            else if (contador < 10000)
                st = "0" + contador.ToString();
            else
                st = contador.ToString();
            using (FileStream fs = new FileStream("results/"+name+"/"+ st + ".png", FileMode.Create))
            {
                encoder.Save(fs);
            }
            frame.Dispose();
            //Console.WriteLine(contador);
            //contador++;

        }

        public static ImageSource ToBitmap(this DepthFrame frame)
        {
            int width = frame.FrameDescription.Width;
            int height = frame.FrameDescription.Height;
            PixelFormat format = PixelFormats.Bgr32;

            ushort minDepth = frame.DepthMinReliableDistance;
            ushort maxDepth = frame.DepthMaxReliableDistance;

            ushort[] pixelData = new ushort[width * height];
            byte[] pixels = new byte[width * height * (format.BitsPerPixel + 7) / 8];

            frame.CopyFrameDataToArray(pixelData);

            int colorIndex = 0;
            for (int depthIndex = 0; depthIndex < pixelData.Length; ++depthIndex)
            {
                ushort depth = pixelData[depthIndex];

                byte intensity = (byte)(depth >= minDepth && depth <= maxDepth ? depth : 0);

                pixels[colorIndex++] = intensity; // Blue
                pixels[colorIndex++] = intensity; // Green
                pixels[colorIndex++] = intensity; // Red

                ++colorIndex;
            }

            int stride = width * format.BitsPerPixel / 8;

            return BitmapSource.Create(width, height, 96, 96, format, null, pixels, stride);
        }

        public static ImageSource ToBitmap(this InfraredFrame frame)
        {
            int width = frame.FrameDescription.Width;
            int height = frame.FrameDescription.Height;
            PixelFormat format = PixelFormats.Bgr32;

            ushort[] frameData = new ushort[width * height];
            byte[] pixels = new byte[width * height * (format.BitsPerPixel + 7) / 8];

            frame.CopyFrameDataToArray(frameData);

            int colorIndex = 0;
            for (int infraredIndex = 0; infraredIndex < frameData.Length; infraredIndex++)
            {
                ushort ir = frameData[infraredIndex];

                byte intensity = (byte)(ir >> 7);

                pixels[colorIndex++] = (byte)(intensity / 1); // Blue
                pixels[colorIndex++] = (byte)(intensity / 1); // Green   
                pixels[colorIndex++] = (byte)(intensity / 0.4); // Red

                colorIndex++;
            }

            int stride = width * format.BitsPerPixel / 8;

            return BitmapSource.Create(width, height, 96, 96, format, null, pixels, stride);
        }

        #endregion

        #region Body

        public static Joint ScaleTo(this Joint joint, double width, double height, float skeletonMaxX, float skeletonMaxY)
        {
            joint.Position = new CameraSpacePoint
            {
                X = Scale(width, skeletonMaxX, joint.Position.X),
                Y = Scale(height, skeletonMaxY, -joint.Position.Y),
                Z = joint.Position.Z
            };

            return joint;
        }

        public static Joint ScaleTo(this Joint joint, double width, double height)
        {
            return ScaleTo(joint, width, height, 1.0f, 1.0f);
        }

        private static float Scale(double maxPixel, double maxSkeleton, float position)
        {
            float value = (float)((((maxPixel / maxSkeleton) / 2) * position) + (maxPixel / 2));

            if (value > maxPixel)
            {
                return (float)maxPixel;
            }

            if (value < 0)
            {
                return 0;
            }

            return value;
        }

        #endregion

        #region Drawing

        public static void DrawSkeleton(this Canvas canvas, Body body, bool recording, string name, KinectSensor sensor, Label labelposture, int segundos, int frame_number)
        {
            if (body == null) return;

            foreach (Joint joint in body.Joints.Values)
            {
                canvas.DrawPoint(joint, sensor);
            }

            canvas.DrawLine(body.Joints[JointType.Head], body.Joints[JointType.Neck], sensor);
            canvas.DrawLine(body.Joints[JointType.Neck], body.Joints[JointType.SpineShoulder], sensor);
            canvas.DrawLine(body.Joints[JointType.SpineShoulder], body.Joints[JointType.ShoulderLeft], sensor);
            canvas.DrawLine(body.Joints[JointType.SpineShoulder], body.Joints[JointType.ShoulderRight], sensor);
            canvas.DrawLine(body.Joints[JointType.SpineShoulder], body.Joints[JointType.SpineMid], sensor);
            canvas.DrawLine(body.Joints[JointType.ShoulderLeft], body.Joints[JointType.ElbowLeft], sensor);
            canvas.DrawLine(body.Joints[JointType.ShoulderRight], body.Joints[JointType.ElbowRight], sensor);
            canvas.DrawLine(body.Joints[JointType.ElbowLeft], body.Joints[JointType.WristLeft], sensor);
            canvas.DrawLine(body.Joints[JointType.ElbowRight], body.Joints[JointType.WristRight], sensor);
            canvas.DrawLine(body.Joints[JointType.WristLeft], body.Joints[JointType.HandLeft], sensor);
            canvas.DrawLine(body.Joints[JointType.WristRight], body.Joints[JointType.HandRight], sensor);
            canvas.DrawLine(body.Joints[JointType.HandLeft], body.Joints[JointType.HandTipLeft], sensor);
            canvas.DrawLine(body.Joints[JointType.HandRight], body.Joints[JointType.HandTipRight], sensor);
            canvas.DrawLine(body.Joints[JointType.HandTipLeft], body.Joints[JointType.ThumbLeft], sensor);
            canvas.DrawLine(body.Joints[JointType.HandTipRight], body.Joints[JointType.ThumbRight], sensor);
            canvas.DrawLine(body.Joints[JointType.SpineMid], body.Joints[JointType.SpineBase], sensor);
            canvas.DrawLine(body.Joints[JointType.SpineBase], body.Joints[JointType.HipLeft], sensor);
            canvas.DrawLine(body.Joints[JointType.SpineBase], body.Joints[JointType.HipRight], sensor);
            canvas.DrawLine(body.Joints[JointType.HipLeft], body.Joints[JointType.KneeLeft], sensor);
            canvas.DrawLine(body.Joints[JointType.HipRight], body.Joints[JointType.KneeRight], sensor);
            canvas.DrawLine(body.Joints[JointType.KneeLeft], body.Joints[JointType.AnkleLeft], sensor);
            canvas.DrawLine(body.Joints[JointType.KneeRight], body.Joints[JointType.AnkleRight], sensor);
            canvas.DrawLine(body.Joints[JointType.AnkleLeft], body.Joints[JointType.FootLeft], sensor);
            canvas.DrawLine(body.Joints[JointType.AnkleRight], body.Joints[JointType.FootRight], sensor);
            int b = posture_classifier(body);
            if (b == 1)
            {
                labelposture.Content = "Good";
            }
            if (b == 2)
            {
                labelposture.Content = "Bad";
            }
            if (b == 0)
            {
                labelposture.Content = "Normal";
            }
            if (recording)
            {
                if (b==1 || b == 2)
                {
                    File.AppendAllText("results/" + name + ".csv", "segundo:" + segundos + ",posture:" + b +",image:"+(frame_number+1)+Environment.NewLine);
                    must_record = true;
                }
                //File.AppendAllText("results/"+name+".csv", "HEAD:" + body.Joints[JointType.Head].Position.X.ToString() + "," + body.Joints[JointType.Head].Position.Y.ToString() + "," + body.Joints[JointType.Head].Position.Z.ToString() + ";NECK:" + body.Joints[JointType.Neck].Position.X.ToString() + "," + body.Joints[JointType.Neck].Position.Y.ToString() + "," + body.Joints[JointType.Neck].Position.X.ToString() + ";SPINESHOULDER:" + body.Joints[JointType.SpineShoulder].Position.X.ToString() + "," + body.Joints[JointType.SpineShoulder].Position.Y.ToString() + "," + body.Joints[JointType.SpineShoulder].Position.X.ToString() + ";SHOULDERLEFT:" + body.Joints[JointType.ShoulderLeft].Position.X.ToString() + "," + body.Joints[JointType.ShoulderLeft].Position.Y.ToString() + "," + body.Joints[JointType.ShoulderLeft].Position.X.ToString() + ";SHOULDERRIGHT:" + body.Joints[JointType.ShoulderRight].Position.X.ToString() + "," + body.Joints[JointType.ShoulderRight].Position.Y.ToString() + "," + body.Joints[JointType.ShoulderRight].Position.X.ToString() + ";SPINEMID:" + body.Joints[JointType.SpineMid].Position.X.ToString() + "," + body.Joints[JointType.SpineMid].Position.Y.ToString() + "," + body.Joints[JointType.SpineMid].Position.X.ToString() + ";ELBOWLEFT:" + body.Joints[JointType.ElbowLeft].Position.X.ToString() + "," + body.Joints[JointType.ElbowLeft].Position.Y.ToString() + "," + body.Joints[JointType.ElbowLeft].Position.X.ToString() + ";ELBOWRIGHT:" + body.Joints[JointType.ElbowRight].Position.X.ToString() + "," + body.Joints[JointType.ElbowRight].Position.Y.ToString() + "," + body.Joints[JointType.ElbowRight].Position.X.ToString() + ";WRISTLEFT:" + body.Joints[JointType.WristLeft].Position.X.ToString() + "," + body.Joints[JointType.WristLeft].Position.Y.ToString() + "," + body.Joints[JointType.WristLeft].Position.X.ToString() + ";WRISTRIGHT:" + body.Joints[JointType.WristRight].Position.X.ToString() + "," + body.Joints[JointType.WristRight].Position.Y.ToString() + "," + body.Joints[JointType.WristRight].Position.X.ToString() + ";HANDLEFT:" + body.Joints[JointType.HandLeft].Position.X.ToString() + "," + body.Joints[JointType.HandLeft].Position.Y.ToString() + "," + body.Joints[JointType.HandLeft].Position.X.ToString() + ";HANDRIGHT:" + body.Joints[JointType.HandRight].Position.X.ToString() + "," + body.Joints[JointType.HandRight].Position.Y.ToString() + "," + body.Joints[JointType.HandRight].Position.X.ToString() + ";HANDTIPLEFT:" + body.Joints[JointType.HandTipLeft].Position.X.ToString() + "," + body.Joints[JointType.HandTipLeft].Position.Y.ToString() + "," + body.Joints[JointType.HandTipLeft].Position.X.ToString() + ";HANDTIPRIGHT:" + body.Joints[JointType.HandTipRight].Position.X.ToString() + "," + body.Joints[JointType.HandTipRight].Position.Y.ToString() + "," + body.Joints[JointType.HandTipRight].Position.X.ToString() + ";THUMBLEFT:" + body.Joints[JointType.ThumbLeft].Position.X.ToString() + "," + body.Joints[JointType.ThumbLeft].Position.Y.ToString() + "," + body.Joints[JointType.ThumbLeft].Position.X.ToString() + ";THUMBRIGHT:" + body.Joints[JointType.ThumbRight].Position.X.ToString() + "," + body.Joints[JointType.ThumbRight].Position.Y.ToString() + "," + body.Joints[JointType.ThumbRight].Position.X.ToString() + ";SPINEBASE:" + body.Joints[JointType.SpineBase].Position.X.ToString() + "," + body.Joints[JointType.SpineBase].Position.Y.ToString() + "," + body.Joints[JointType.SpineBase].Position.X.ToString() + ";HIPLEFT:" + body.Joints[JointType.HipLeft].Position.X.ToString() + "," + body.Joints[JointType.HipLeft].Position.Y.ToString() + "," + body.Joints[JointType.HipLeft].Position.X.ToString() + ";HIPRIGHT:" + body.Joints[JointType.HipRight].Position.X.ToString() + "," + body.Joints[JointType.HipRight].Position.Y.ToString() + "," + body.Joints[JointType.HipRight].Position.X.ToString() + ";KNEELEFT:" + body.Joints[JointType.KneeLeft].Position.X.ToString() + "," + body.Joints[JointType.KneeLeft].Position.Y.ToString() + "," + body.Joints[JointType.KneeLeft].Position.X.ToString() + ";KNEERIGHT:" + body.Joints[JointType.KneeRight].Position.X.ToString() + "," + body.Joints[JointType.KneeRight].Position.Y.ToString() + "," + body.Joints[JointType.KneeRight].Position.X.ToString() + ";ANKLELEFT:" + body.Joints[JointType.AnkleLeft].Position.X.ToString() + "," + body.Joints[JointType.AnkleLeft].Position.Y.ToString() + "," + body.Joints[JointType.AnkleLeft].Position.X.ToString() + ";ANKLERIGHT:" + body.Joints[JointType.AnkleRight].Position.X.ToString() + "," + body.Joints[JointType.AnkleRight].Position.Y.ToString() + "," + body.Joints[JointType.AnkleRight].Position.X.ToString() + ";FOOTLEFT:" + body.Joints[JointType.FootLeft].Position.X.ToString() + "," + body.Joints[JointType.FootLeft].Position.Y.ToString() + "," + body.Joints[JointType.FootLeft].Position.X.ToString() + ";FOOTRIGHT:" + body.Joints[JointType.FootRight].Position.X.ToString() + "," + body.Joints[JointType.FootRight].Position.Y.ToString() + "," + body.Joints[JointType.FootRight].Position.X.ToString()+"\n");

            }
        }

        public static int posture_classifier(Body body)
        {
            if (body.Joints[JointType.WristLeft].Position.Y > body.Joints[JointType.SpineMid].Position.Y - 0.15 || body.Joints[JointType.WristRight].Position.Y > body.Joints[JointType.SpineMid].Position.Y - 0.15)
                return 1;
            if (body.Joints[JointType.WristLeft].Position.Y < body.Joints[JointType.SpineMid].Position.Y && body.Joints[JointType.WristRight].Position.Y < body.Joints[JointType.SpineMid].Position.Y)
                return 2;
            if (body.Joints[JointType.WristLeft].Position.Y < body.Joints[JointType.SpineBase].Position.Y && body.Joints[JointType.WristRight].Position.Y < body.Joints[JointType.SpineBase].Position.Y)
                return 2;
            if (body.Joints[JointType.WristLeft].Position.Y > body.Joints[JointType.SpineMid].Position.Y || body.Joints[JointType.WristRight].Position.Y > body.Joints[JointType.SpineMid].Position.Y)
                return 1;
            return 0;
        }

        public static void DrawPoint(this Canvas canvas, Joint joint, KinectSensor sensor)
        {
            if (joint.TrackingState == TrackingState.NotTracked) return;

            CameraSpacePoint jointPosition = joint.Position;
            ColorSpacePoint colorPoint = sensor.CoordinateMapper.MapCameraPointToColorSpace(jointPosition);

            Point point = new Point();
            point.X = float.IsInfinity(colorPoint.X) ? 0 : colorPoint.X;
            point.Y = float.IsInfinity(colorPoint.Y) ? 0 : colorPoint.Y;
            joint = joint.ScaleTo(canvas.ActualWidth, canvas.ActualHeight);

            Ellipse ellipse = new Ellipse
            {
                Width = 20,
                Height = 20,
                Fill = new SolidColorBrush(Colors.LightBlue)
            };

            //Canvas.SetLeft(ellipse, joint.Position.X - ellipse.Width / 2);
            //Canvas.SetTop(ellipse, joint.Position.Y - ellipse.Height / 2);
            Canvas.SetLeft(ellipse, point.X - ellipse.Width / 2);
            Canvas.SetTop(ellipse, point.Y - ellipse.Height / 2);
            canvas.Children.Add(ellipse);
        }

        public static void DrawLine(this Canvas canvas, Joint first, Joint second, KinectSensor sensor)
        {
            if (first.TrackingState == TrackingState.NotTracked || second.TrackingState == TrackingState.NotTracked) return;

            // 3D space point
            CameraSpacePoint firstjointPosition = first.Position;
            CameraSpacePoint secondjointPosition = second.Position;
            // 2D space point
            Point firstpoint = new Point();
            Point secondpoint = new Point();
            ColorSpacePoint colorPoint = sensor.CoordinateMapper.MapCameraPointToColorSpace(firstjointPosition);
            ColorSpacePoint secondcolorPoint = sensor.CoordinateMapper.MapCameraPointToColorSpace(secondjointPosition);

            firstpoint.X = float.IsInfinity(colorPoint.X) ? 0 : colorPoint.X;
            firstpoint.Y = float.IsInfinity(colorPoint.Y) ? 0 : colorPoint.Y;
            secondpoint.X = float.IsInfinity(secondcolorPoint.X) ? 0 : secondcolorPoint.X;
            secondpoint.Y = float.IsInfinity(secondcolorPoint.Y) ? 0 : secondcolorPoint.Y;

            first = first.ScaleTo(canvas.ActualWidth, canvas.ActualHeight);
            second = second.ScaleTo(canvas.ActualWidth, canvas.ActualHeight);

            //Line line = new Line
            //{
            //    X1 = first.Position.X,
            //    Y1 = first.Position.Y,
            //    X2 = second.Position.X,
            //    Y2 = second.Position.Y,
            //    StrokeThickness = 8,
            //    Stroke = new SolidColorBrush(Colors.LightBlue)
            //};

            Line line = new Line
            {
                X1 = firstpoint.X,
                Y1 = firstpoint.Y,
                X2 = secondpoint.X,
                Y2 = secondpoint.Y,
                StrokeThickness = 8,
                Stroke = new SolidColorBrush(Colors.LightBlue)
            };
            canvas.Children.Add(line);
        }

        #endregion
    }
}
