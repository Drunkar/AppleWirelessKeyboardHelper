using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using IronPython;
using Microsoft.Scripting;
using Microsoft.Scripting.Hosting;

namespace Misuzilla.Applications.AppleWirelessKeyboardHelper
{
    public class Program
    {
        private static NotifyIcon _notifyIcon;
        private const String ApplicationName = "Apple Wireless Keyboard Helper";
        private static IScriptModule _module;

        public static Int32 BalloonTipTimeout = 1500;

        private const UInt32 JISAlphaNumericKeyScanCode = 113; // 113
        private const UInt32 JISKanaKeyScanCode = 114; // 114
        /// <summary>
        /// �n���h������Ă��Ȃ���O���L���b�`���āA�X�^�b�N�g���[�X��ۑ����ăf�o�b�O�ɖ𗧂Ă�
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = e.ExceptionObject as Exception; // Exception �ȊO�����ł���̂͒�����ȏꍇ�̂݁B

            if (MessageBox.Show(
                String.Format("�A�v���P�[�V�����̎��s���ɗ\�����Ȃ��d��ȃG���[���������܂����B\n\n�G���[���e:\n{0}\n\n�G���[�����t�@�C���ɕۑ����A�񍐂��Ă����������Ƃŕs��̉����ɖ𗧂\��������܂��B�G���[�����t�@�C���ɕۑ����܂���?",
                ((Exception)(e.ExceptionObject)).Message)
                , Application.ProductName
                , MessageBoxButtons.YesNo
                , MessageBoxIcon.Error
                , MessageBoxDefaultButton.Button1
                ) == DialogResult.Yes)
            {
                using (SaveFileDialog saveFileDialog = new SaveFileDialog())
                {
                    //saveFileDialog.DefaultExt = "txt";
                    //saveFileDialog.Filter = "�e�L�X�g�t�@�C��|*.txt";
                    //saveFileDialog.FileName = String.Format("AppleWirelessKeyboardHelper_Stacktrace_{0:yyyyMMdd_HHmmss}.txt", DateTime.Now);
                    //if (saveFileDialog.ShowDialog() == DialogResult.OK)
                    {
                        String filePath = String.Format(Path.Combine(Assembly.GetExecutingAssembly().CodeBase, "AppleWirelessKeyboardHelper_Stacktrace_{0:yyyyMMdd_HHmmss}.txt"), DateTime.Now);
                        using (Stream stream = File.Open(filePath, FileMode.CreateNew))
                        using (StreamWriter sw = new StreamWriter(stream))
                        {
                            Assembly asm = Assembly.GetExecutingAssembly();

                            sw.WriteLine("��������: {0}", DateTime.Now);
                            sw.WriteLine();
                            sw.WriteLine("AppleWirelessKeyboardHelper:");
                            sw.WriteLine("========================");
                            sw.WriteLine("�o�[�W����: {0}", Assembly.GetExecutingAssembly().GetName().Version);
                            sw.WriteLine("�A�Z���u��: {0}", Assembly.GetExecutingAssembly().FullName);
                            sw.WriteLine();
                            sw.WriteLine("�����:");
                            sw.WriteLine("========================");
                            sw.WriteLine("�I�y���[�e�B���O�V�X�e��: {0}", Environment.OSVersion);
                            sw.WriteLine("Microsoft .NET Framework: {0}", Environment.Version);
                            sw.WriteLine("IntPtr: {0}", IntPtr.Size);
                            sw.WriteLine();
                            sw.WriteLine("�n���h������Ă��Ȃ���O: ");
                            sw.WriteLine("=========================");
                            sw.WriteLine(ex.ToString());
                        }
                    }
                }

            }
        }

        //[STAThread]
        static void Main()
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            using (Helper helper = new Helper())
            {
                // TypeLib ��� IDispatch ��D�悷��
                ((PythonEngineOptions)(Script.GetEngine("py").Options)).PreferComDispatchOverTypeInfo = true;

                helper.FnKeyCombinationDown += delegate(Object sender, AppleKeyboardEventArgs e)
                {
                    StringBuilder funcName = new StringBuilder("OnDown");
                    if (e.AppleKeyState == AppleKeyboardKeys.Fn)
                        funcName.Append("_Fn");
                    if (e.AppleKeyState == AppleKeyboardKeys.Eject)
                        funcName.Append("_Eject");

                    funcName.Append("_").Append(e.Key.ToString());

                    Call(funcName.ToString(), e);

                    e.Handled = true;
                };

                helper.KeyUp += delegate(Object sender, AppleKeyboardEventArgs e)
                {
                    if (e.KeyEventStruct.wScan != JISAlphaNumericKeyScanCode && e.KeyEventStruct.wScan != JISKanaKeyScanCode)
                        return;
                    
                    StringBuilder funcName = new StringBuilder("OnUp");
                    if (e.AppleKeyState == AppleKeyboardKeys.Fn)
                        funcName.Append("_Fn");
                    if (e.AppleKeyState == AppleKeyboardKeys.Eject)
                        funcName.Append("_Eject");

                    funcName.Append("_").Append((e.KeyEventStruct.wScan == JISAlphaNumericKeyScanCode) ? "JISAlphaNumeric" : "JISKana");

                    Call(funcName.ToString(), e);

                    e.Handled = true;
                };

                helper.SpecialKeyDown += delegate(Object sender, KeyEventArgs e)
                {
                    StringBuilder funcName = new StringBuilder("OnDown");
                    if (e.AppleKeyboardKey == AppleKeyboardKeys.Fn)
                        funcName.Append("_Fn");
                    if (e.AppleKeyboardKey == AppleKeyboardKeys.Eject)
                        funcName.Append("_Eject");
                    if (e.IsPowerButtonDown)
                        funcName.Append("_Power");

                    Call(funcName.ToString(), e);
                };

                helper.Disconnected += delegate
                {
                    ShowBalloonTip(Resources.Strings.KeyboardDisconnected, ToolTipIcon.Warning);
                    helper.Shutdown();
                    while (!helper.Start())
                    {
                        // retry at interval of 10sec
                        System.Threading.Thread.Sleep(10000);
                    }
                    ShowBalloonTip(Resources.Strings.KeyboardConnected, ToolTipIcon.Info);
                };

                if (!helper.Start())
                {
                    MessageBox.Show(Resources.Strings.KeyboardNotConnected, ApplicationName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                helper.Hook();

                SetupNotifyIcon();
                LoadScripts();

                Application.Run();

                _notifyIcon.Visible = false;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="funcName"></param>
        /// <param name="e"></param>
        private static void Call(String funcName, EventArgs e)
        {
            Object funcObj;
            if (!_module.TryLookupVariable(funcName, out funcObj))
                return;
            
            FastCallable f = funcObj as FastCallable;
            if (f == null)
                return;
            try
            {
                f.Call(InvariantContext.CodeContext);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), ApplicationName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        
        /// <summary>
        /// 
        /// </summary>
        private static void LoadScripts()
        {
            // �Â����̂����ׂč폜
            OnUnload(EventArgs.Empty);
            Unload = null;
            Load = null;

#pragma warning disable 0618
            DynamicHelpers.TopNamespace.LoadAssembly(Assembly.GetExecutingAssembly());
            DynamicHelpers.TopNamespace.LoadAssembly(Assembly.LoadWithPartialName("System.Windows.Forms"));
#pragma warning restore 0618

            IScriptEnvironment scriptEnv = ScriptEnvironment.GetEnvironment();
            _module = scriptEnv.CreateModule("ScriptModule");

            Boolean hasScripts = false;
            if (Directory.Exists("Scripts"))
            {
                foreach (String path in Directory.GetFiles("Scripts", "*.py"))
                {
                    Debug.WriteLine("Load Script: " + path);
                    try
                    {
                        IScriptEngine engine = scriptEnv.GetLanguageProviderByFileExtension(Path.GetExtension(path)).GetEngine();
                        engine.ExecuteFileContent(path, _module);
                        hasScripts = true;
                    }
                    catch (SyntaxErrorException se)
                    {
                        MessageBox.Show(String.Format(Resources.Strings.ScriptSyntaxException, path, se.Line, se.Column, se.Message), ApplicationName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show(String.Format(Resources.Strings.ScriptException, path, e.Message), ApplicationName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }

            // ����ǂݍ���ł��Ȃ�������f�t�H���g
            if (!hasScripts)
            {
                Script.GetEngine("py").Execute(Resources.Strings.DefaultPythonScript, _module);
            }

            // ���s
            _module.Execute();
            
            OnLoad(EventArgs.Empty);
            
            ShowBalloonTip(Resources.Strings.ScriptsLoaded, ToolTipIcon.Info);
        }

        /// <summary>
        /// 
        /// </summary>
        private static void SetupNotifyIcon()
        {
            _notifyIcon = new NotifyIcon();
            _notifyIcon.Icon = Resources.Common.AppleWirelessKeyboardHelperTrayIcon16x16;
            _notifyIcon.Text = ApplicationName;
            _notifyIcon.Visible = true;
            _notifyIcon.ContextMenu = new ContextMenu(new MenuItem[]{
                new MenuItem(Resources.Strings.MenuItemReloadScripts, delegate {
                    LoadScripts();
                })
                , new MenuItem("-")
                , new MenuItem(Resources.Strings.MenuItemExit, delegate {
                    Application.Exit();
                })
            });
        }

        public static void ShowBalloonTip(String text)
        {
            ShowBalloonTip(text, ToolTipIcon.Info);
        }
        
        public static void ShowBalloonTip(String text, ToolTipIcon toolTipIcon)
        {
            _notifyIcon.ShowBalloonTip(BalloonTipTimeout, ApplicationName, text, toolTipIcon);
        }

        public static event EventHandler Unload;
        private static void OnUnload(EventArgs e)
        {
            try
            {
                if (Unload != null)
                    Unload(new Object(), e);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
        }
        public static event EventHandler Load;
        private static void OnLoad(EventArgs e)
        {
            try
            {
                if (Load != null)
                    Load(new Object(), e);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
        }
    }
}
