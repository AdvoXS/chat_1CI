using System;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.IO;
using System.Security.Cryptography;
namespace chatick
{
    public partial class Form1 : Form
    {
        //setup
        IPEndPoint remote;
        UdpClient client;
        IPAddress multiaddress;
        Random rand = new Random();
        int random;
        string name; //user name

        //listen
        IPEndPoint localIp;
        UdpClient udpClient;
        Thread thread;

        //listener
        string formatted_data;

        //form
        bool formclosed = false;
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            random = rand.Next(1, 1000);
            name = "user" + random;
            titul lul = new titul();
            lul.ShowDialog();
            lul.Activate();
            setupForm setForm = new setupForm(this);
            setForm.ShowDialog();
            setup();
        }

        //--------------------------------------backend---------------------------------------------------------------

        private void setup()
        {
            string ipAddress = "239.0.0.222"; //ip адрес предоставляемый Windows для локального udp доступа
            multiaddress = IPAddress.Parse(ipAddress);
            client = new UdpClient();
            client.JoinMulticastGroup(multiaddress);//входим в локальную многопользовательскую группу
            remote = new IPEndPoint(multiaddress, 2222);
            
            label4.Text = "Ник: " + name;

            Listen();
        }


        private void Listen()
        {
            udpClient = new UdpClient();
            udpClient.Client.SendTimeout = 5000;
            udpClient.Client.ReceiveTimeout = 500;
            udpClient.ExclusiveAddressUse = false;
            localIp = new IPEndPoint(IPAddress.Any, 2222);

            udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udpClient.ExclusiveAddressUse = false;
            udpClient.Client.Bind(localIp);

            udpClient.JoinMulticastGroup(multiaddress);

            textBox1.Text = "";
            button3.Visible = true;
            textBox3.Visible = true;

            //connectInfo
            connectMessage();
            listUsers.Items.Add(name + "(Вы)");
            sendName();
            requestNames();

            //начинаем слушать сеть
            thread = new Thread(new ThreadStart(Listener));
            thread.Start();
            textBox3.Focus();
             }



        

        private void Listener()
        {
            string tmpName = "";
            try
            {
                while (true)
                {
                    Byte[] data;
                    data = udpClient.Receive(ref localIp); //принимаем
                    formatted_data = FromAes256(data); //расшифрование

                    //принятие имени в список участников

                    if (formatted_data != "" && formatted_data[0] == '%' && formatted_data[1] == '&' && formatted_data[2] == 'n' && formatted_data[3] == 'm')
                    {
                        for (int i = 4; i < formatted_data.Length; i++)
                        {
                            tmpName += formatted_data[i];
                        }
                        int indName = -1;
                        indName = listUsers.FindString(tmpName);
                        if (indName == -1)
                        {
                            Action act = () => listUsers.Items.Add(tmpName);
                            listUsers.Invoke(act);

                        }

                        tmpName = "";

                    }
                    //запрос на передачу своего имени
                    else if (formatted_data != "" && formatted_data[0] == 'r' && formatted_data[1] == '@' && formatted_data[2] == 'q' && formatted_data[3] == '#' && formatted_data[4] == 's' && formatted_data[5] == '+' && formatted_data[6] == '+')
                    {
                        sendName();
                    }
                    //удаление из списка участников
                    else if (formatted_data != "" && formatted_data[0] == 'd' && formatted_data[1] == '%' && formatted_data[2] == 'e' && formatted_data[3] == '?' && formatted_data[4] == 'l')
                    {
                        for (int i = 5; i < formatted_data.Length; i++)
                        {
                            tmpName += formatted_data[i];
                        }
                        int indName = -1;
                        indName = listUsers.FindString(tmpName);
                        if (indName != -1)
                        {
                            Action act = () => listUsers.Items.RemoveAt(indName);
                            listUsers.Invoke(act);
                        }
                        tmpName = "";
                    }
                    //вывод обычных данных
                    else if (formatted_data != "")
                    {
                        Action act = () => textBox1.AppendText(formatted_data + "\r\n");
                        textBox1.Invoke(act);
                        Action act1 = () => textBox3.Focus();
                        textBox3.Invoke(act1);

                    }



                }
            }
            catch (System.Net.Sockets.SocketException e) //сбрасываем соединение при проблемах
            {
                if (e.ErrorCode != 10060) 
                {

                    delName();
                    udpClient.Close();
                    thread.Abort();
                    thread.Join(5000);
                    thread = null;
                }
                else if (e.ErrorCode == 10060) //если таймаут
                {
                    if (formclosed == false) Listener(); //форма не закрыта -> продолжаем приём
                    else
                    {
                        delName();
                        udpClient.Close();
                        thread.Abort();
                        thread.Join(5000);
                        thread = null;
                    }
                }

            }
        }
        
        // функции отправки данных
        private void SEND(string mes)
        {
            byte[] data = ToAes256(mes);
            udpClient.Send(data, data.Length, remote);
        }
        private void sendMessage(string mes)
        {
            string kek = name + ": " + mes;
            SEND(kek);
            textBox3.Text = "";
        }
        
            private void connectMessage()
        {
            string kek = "\t\t\t" + name + " connected to chat";
            SEND(kek);
        }
        private void sendName()
        {
            string kek = "%&nm" + name;
            SEND(kek);
        }
        private void requestNames()
        {
            string kek = "r@q#s++";
            SEND(kek);
        }
        private void delName()
        {
            string kek = "d%e?l" + name;
            SEND(kek);
            discMessage();
        }
        private void discMessage()
        {
            string kek = "\t\t\t" + name + " disconnected from chat";
            SEND(kek);
        }


        public void setName(string name)
        {
            this.name = name;
        }


        //events funcs

        private void button3_Click(object sender, EventArgs e)
        {
            if (textBox3.Text != "") sendMessage(textBox3.Text);
            textBox3.Focus();
        }
        private void textBox3_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == 13)
            {
                if (textBox3.Text != "") sendMessage(textBox3.Text);
                textBox3.Focus();
            }
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            formclosed = true;
        }
        
        private void exitToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            delName();
            udpClient.Close();
            thread.Abort();
            thread.Join(5000);
            thread = null;

            Application.Exit();
        }

        int countChatSaves = 0;
        private void saveChatFromFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if(alert)
            MessageBox.Show("Вы сохраняете файл в незащищенном режиме!", "Внимание", MessageBoxButtons.OK,
                                MessageBoxIcon.Information);
            SaveFileDialog sf = new SaveFileDialog();
            sf.Filter = "txt files (*.txt)|*.txt|All files (*.*)|*.*";
            sf.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            DateTime thisFay = DateTime.Today;
            sf.FileName = "Chat #" + countChatSaves + " " + thisFay.ToString("d");
            if (sf.ShowDialog() == DialogResult.OK)
            {
                StreamWriter sw = new StreamWriter(sf.FileName);
                string text = textBox1.Text;
                sw.Write(text);
                sw.Close();
                
                MessageBox.Show("Чат сохранён в файл: " + sf.FileName);
                countChatSaves++;
            }

        }



        
        private void clearChatToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            textBox1.Text = "";
            textBox3.Focus();
        }

        private void Form1_Activated(object sender, EventArgs e)
        {
            textBox3.Focus();
        }


        static string key = "W1s)g+,ER012Bba@g89!!dwn56F8X0xr"; // ключ шифрование статичен :)
        static string IV = "4FghvE$##vBBnmZ!"; // соль
        public static byte[] ToAes256(string src)
        {

            Aes aes = Aes.Create();
            byte[] encrypted;
            ICryptoTransform crypt = aes.CreateEncryptor(Encoding.ASCII.GetBytes(key), Encoding.ASCII.GetBytes(IV));
            using (MemoryStream ms = new MemoryStream())
            {
                using (CryptoStream cs = new CryptoStream(ms, crypt, CryptoStreamMode.Write))
                {
                    using (StreamWriter sw = new StreamWriter(cs))
                    {
                        sw.Write(src);
                    }
                }

                encrypted = ms.ToArray();
            }

            return encrypted.Concat(Encoding.ASCII.GetBytes(IV)).ToArray();
        }

        public static string FromAes256(byte[] shifr)
        {
            byte[] bytesIv = new byte[16];
            //Списываем соль

            byte[] mess = new byte[shifr.Length - 16];
            for (int i = shifr.Length - 16, j = 0; i < shifr.Length; i++, j++)
                bytesIv[j] = shifr[i];

            //создаем исключения
            if (shifr == null || shifr.Length <= 0)
                throw new ArgumentNullException("shifr");
            if (key == null || key.Length <= 0)
                throw new ArgumentNullException("Key");
            if (Encoding.UTF8.GetString(bytesIv) == null || Encoding.ASCII.GetString(bytesIv).Length <= 0)
                throw new ArgumentNullException("IV");

             for (int i = 0; i < shifr.Length - 16; i++)
                mess[i] = shifr[i]; //получаем зашифрованное сообщение без "соли"

            Aes aes = Aes.Create();

             aes.IV = bytesIv;

            string text = "";
            byte[] data = mess;
            ICryptoTransform crypt = aes.CreateDecryptor(Encoding.ASCII.GetBytes(key), aes.IV);
            using (MemoryStream ms = new MemoryStream(data))
            {
                using (CryptoStream cs = new CryptoStream(ms, crypt, CryptoStreamMode.Read))
                {
                    using (StreamReader sr = new StreamReader(cs))
                    {
                        try
                        {
                            //Результат записываем в переменную text в виде исходной строки
                            text = sr.ReadToEnd();
                        }
                        catch
                        {
                            MessageBox.Show("Неправильно открыт файл!","Ошибка", MessageBoxButtons.OK,
                                 MessageBoxIcon.Error);
                            Application.Restart();
                        }
                    }
                }
            }
            return text;
        }

        private void выходToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void сохранитьВФайлзащищенныйРежимToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog sf = new SaveFileDialog();
            sf.Filter = "txt files (*.txt)|*.txt|All files (*.*)|*.*";
            sf.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            DateTime thisFay = DateTime.Today;
            sf.FileName = "Chat(s) #" + countChatSaves + " " + thisFay.ToString("d");
            if (sf.ShowDialog() == DialogResult.OK)
            {
                FileStream fsFileIn = File.OpenWrite(sf.FileName);
               
                string text = textBox1.Text;
                byte[] data = ToAes256(text);
                for (int i = 0; i < data.Length; i++) {fsFileIn.WriteByte(data[i]); }
                
                fsFileIn.Close();
                MessageBox.Show("Чат сохранён в файл: " + sf.FileName);
                countChatSaves++;
            }
        }

        private void открытьФайлзащищенныйРежимToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DialogResult result = System.Windows.Forms.DialogResult.OK;
            if (alert)
           result = MessageBox.Show("Перед открытием файла удостоверьтесь, что файл был сохранен в защищенном режиме. В противном случае файл может быть открыт некорректно.", "Предупреждение", MessageBoxButtons.OKCancel,
                                 MessageBoxIcon.Warning);
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                OpenFileDialog of = new OpenFileDialog();
                of.Filter = "txt files (*.txt)|*.txt|All files (*.*)|*.*";
                of.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments); 
                if (of.ShowDialog() == DialogResult.OK)
                {
                          byte[] data = File.ReadAllBytes(of.FileName);
                    try
                    {
                        string text = FromAes256(data);
                        openFile open = new openFile(text);


                        open.Show();
                    }
                    catch
                    {
                        MessageBox.Show("Ошибка при чтении файла. Проверьте открываемый файл.","Ошибка", MessageBoxButtons.OK,MessageBoxIcon.Error);
                    }
                }
            }

        }

        private void открытьФайлToolStripMenuItem_Click(object sender, EventArgs e)
        {
            
            OpenFileDialog of = new OpenFileDialog();
            of.Filter = "txt files (*.txt)|*.txt|All files (*.*)|*.*";
            of.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (of.ShowDialog() == DialogResult.OK)
            {
                StreamReader sw = new StreamReader(of.FileName);
                string text = sw.ReadToEnd();
                sw.Close();
                openFile open = new openFile(text);
                open.Show();
                
            }
        }

        private void чтоТакоеЗащищенныйРежимToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Защищенный режим – это защита данных путём их шифрования.\n\nЧат всегда работает в защищенном режиме!\n\nОткрытие файла в защищенном режиме является расшифрование ранее зашифрованных данных и их открытие.\nСохранение файла в защищенном режиме является шифрование данных и их сохранение в файле."
, "Помощь", MessageBoxButtons.OK,
                                 MessageBoxIcon.Asterisk);
        }

        private void показыватьУчастниковЧатаToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (показыватьУчастниковЧатаToolStripMenuItem.Checked)
            {
                label1.Visible = true;
                listUsers.Visible = true;
            }
            else
            {
                label1.Visible = false;
                listUsers.Visible = false;
            }
        }

        private void показыватьМойНикToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (показыватьМойНикToolStripMenuItem.Checked) label4.Visible = true;
            else label4.Visible = false;
        }

        private void оПрограммеToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Narrow chat - это защищенный локальный чат.\n Ivan Borisov, Dubna 2018", "О программе", MessageBoxButtons.OK,
                                 MessageBoxIcon.Information);
        }

       
        bool alert = true;
        private void показыватьПредупрежденияБезопасностиToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!показыватьПредупрежденияБезопасностиToolStripMenuItem.Checked) alert = false;
            else alert = true;
        }

        private void textBox1_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == 13) textBox3.Focus();
        }

        private void statusToolStripMenuItem_Click(object sender, EventArgs e)
        {
            stateForm f = new stateForm();
            f.Show();
        }
    }
}
    




