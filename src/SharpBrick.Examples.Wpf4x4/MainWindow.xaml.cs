using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Extensions.DependencyInjection;
using SharpBrick.PoweredUp;
using SharpBrick.PoweredUp.Functions;

namespace SharpBrick.Examples.Wpf4x4
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private double steeringDirection;
        private double speed;
        private double setupProgress;
        private double steeringFrom;
        private double steeringTo;
        private TechnicMediumHub _hub;
        private TechnicXLargeLinearMotor _motor;
        private TechnicLargeLinearMotor _steeringMotor;

        public double SteeringDirection { get => steeringDirection; set { steeringDirection = value; OnPropertyChanged(nameof(SteeringDirection)); } }
        public double SteeringFrom { get => steeringFrom; set { steeringFrom = value; OnPropertyChanged(nameof(SteeringFrom)); } }
        public double SteeringTo { get => steeringTo; set { steeringTo = value; OnPropertyChanged(nameof(SteeringTo)); } }
        public double Speed { get => speed; set { speed = value; OnPropertyChanged(nameof(Speed)); } }
        public double SetupProgress { get => setupProgress; set { setupProgress = value; OnPropertyChanged(nameof(SetupProgress)); } }


        protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        public event PropertyChangedEventHandler PropertyChanged;

        public MainWindow()
        {
            InitializeComponent();

            Task.Run(InitHostAsync); // offload from UI thread

            DataContext = this;

            this.PropertyChanged += (sender, ea) =>
            {
                if (ea.PropertyName == nameof(Speed))
                {
                    Task.Run(async () => await SetSpeedAsync((sbyte)Speed)); // offload from UI thread
                }
                if (ea.PropertyName == nameof(SteeringDirection))
                {
                    Task.Run(async () => await SetDirectionAsync((int)SteeringDirection)); // offload from UI thread
                }
            };
        }

        private async Task InitHostAsync()
        {
            var serviceProvider = new ServiceCollection()
                .AddLogging()
                .AddPoweredUp()
                .AddWinRTBluetooth()
                .BuildServiceProvider();

            var host = serviceProvider.GetService<PoweredUpHost>();

            SetupProgress = 1;

            _hub = await host.DiscoverAsync<TechnicMediumHub>(); // starting version 2.1
            SetupProgress = 2;

            await _hub.ConnectAsync();
            SetupProgress = 3;

            _motor = (await _hub.CreateVirtualPortAsync(0, 1)).GetDevice<TechnicXLargeLinearMotor>();
            _steeringMotor = _hub.C.GetDevice<TechnicLargeLinearMotor>();
            SetupProgress = 4;

            var calibration = _hub.ServiceProvider.GetService<LinearMidCalibration>();
            calibration.MaxPower = 30;
            await calibration.ExecuteAsync(_steeringMotor);

            SteeringFrom = -1 * calibration.ExtendResult;
            SteeringTo = calibration.ExtendResult;

            SetupProgress = 5;
        }

        private async Task SetDirectionAsync(int steeringDirection)
        {
            if (_steeringMotor != null)
            {
                await (_steeringMotor?.GotoPositionAsync(Directions.CCW * steeringDirection, 50, 20, SpecialSpeed.Hold, SpeedProfiles.None));
            }
        }

        private async Task SetSpeedAsync(sbyte speed)
        {
            if (_motor != null)
            {
                await _motor.StartSpeedAsync((sbyte)(Directions.CCW * speed), 100, SpeedProfiles.None);
            }
        }

        protected override async void OnClosing(CancelEventArgs e)
        {
            if (_hub != null)
            {
                await _hub.SwitchOffAsync();
            }
        }
    }
}
