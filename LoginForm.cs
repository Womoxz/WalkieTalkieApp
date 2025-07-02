using System;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Windows.Forms;

namespace WalkieTalkieApp
{
    public partial class LoginForm : Form
    {
        public string UserName { get; private set; }
        public string IPAddress { get; private set; }

        public LoginForm()
        {
            InitializeComponent();
            txtIP.Text = GetLocalIPAddress();
        }

        private string GetLocalIPAddress()
        {
            try
            {
                // Intentar obtener IP activa
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus == OperationalStatus.Up &&
                        ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    {
                        foreach (UnicastIPAddressInformation ip in ni.GetIPProperties().UnicastAddresses)
                        {
                            if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                            {
                                return ip.Address.ToString();
                            }
                        }
                    }
                }
                return "127.0.0.1";
            }
            catch
            {
                return "127.0.0.1";
            }
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtUserName.Text))
            {
                MessageBox.Show("Por favor ingrese su nombre de usuario", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            UserName = txtUserName.Text.Trim();
            IPAddress = txtIP.Text;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}