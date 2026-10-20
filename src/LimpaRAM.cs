// LimpaRAM - abre, aperta o botão e pronto.
// Libera: working sets dos processos, cache de arquivos do sistema,
// lista "modificada" e lista "standby" (o mesmo que o RAMMap faz).
// Precisa rodar como administrador (o manifesto já pede isso).

using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

class LimpaRAM : Form
{
    // ---------- APIs do Windows ----------
    [StructLayout(LayoutKind.Sequential)]
    struct MEMORYSTATUSEX
    {
        public uint dwLength, dwMemoryLoad;
        public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile,
                     ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    struct TOKEN_PRIVILEGES { public int Count; public long Luid; public int Attr; }

    [DllImport("kernel32.dll")] static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX m);
    [DllImport("psapi.dll")] static extern bool EmptyWorkingSet(IntPtr hProcess);
    [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool SetSystemFileCacheSize(IntPtr min, IntPtr max, int flags);
    [DllImport("ntdll.dll")] static extern int NtSetSystemInformation(int infoClass, ref int info, int length);
    [DllImport("advapi32.dll", SetLastError = true)] static extern bool OpenProcessToken(IntPtr h, uint access, out IntPtr token);
    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)] static extern bool LookupPrivilegeValue(string sys, string name, out long luid);
    [DllImport("advapi32.dll", SetLastError = true)] static extern bool AdjustTokenPrivileges(IntPtr token, bool disableAll, ref TOKEN_PRIVILEGES state, int len, IntPtr prev, IntPtr retLen);

    const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
    const uint PROCESS_SET_QUOTA = 0x0100;
    const int SystemMemoryListInformation = 80;
    const int MemoryFlushModifiedList = 3;
    const int MemoryPurgeStandbyList = 4;

    // ---------- Interface ----------
    Label lblUso, lblStatus;
    ProgressBar barra;
    Button btn;
    Timer timer;

    LimpaRAM()
    {
        Text = "LimpaRAM";
        ClientSize = new Size(380, 250);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(28, 30, 36);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10f);

        lblUso = new Label { Left = 20, Top = 18, Width = 340, Height = 26, Font = new Font("Segoe UI", 12f, FontStyle.Bold) };
        barra = new ProgressBar { Left = 20, Top = 50, Width = 340, Height = 18, Maximum = 100 };

        btn = new Button
        {
            Text = "LIMPAR RAM",
            Left = 20, Top = 85, Width = 340, Height = 80,
            Font = new Font("Segoe UI", 18f, FontStyle.Bold),
            BackColor = Color.FromArgb(0, 150, 110),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.Click += Limpar;

        lblStatus = new Label { Left = 20, Top = 178, Width = 340, Height = 60, ForeColor = Color.FromArgb(180, 190, 200) };
        lblStatus.Text = "Pronto. Aperte o botão para liberar memória.";

        Controls.AddRange(new Control[] { lblUso, barra, btn, lblStatus });

        timer = new Timer { Interval = 1000 };
        timer.Tick += delegate { AtualizarUso(); };
        timer.Start();
        AtualizarUso();
    }

    static MEMORYSTATUSEX Status()
    {
        var m = new MEMORYSTATUSEX();
        m.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
        GlobalMemoryStatusEx(ref m);
        return m;
    }

    static string GB(ulong bytes) { return (bytes / 1073741824.0).ToString("0.00") + " GB"; }

    void AtualizarUso()
    {
        var m = Status();
        lblUso.Text = string.Format("Em uso: {0} de {1} ({2}%)",
            GB(m.ullTotalPhys - m.ullAvailPhys), GB(m.ullTotalPhys), m.dwMemoryLoad);
        barra.Value = (int)Math.Min(100, m.dwMemoryLoad);
    }

    void Limpar(object sender, EventArgs e)
    {
        btn.Enabled = false;
        btn.Text = "LIMPANDO...";
        lblStatus.Text = "Liberando memória, aguarde...";
        ulong livreAntes = Status().ullAvailPhys;

        Task.Factory.StartNew(() =>
        {
            int processos = ExecutarLimpeza();
            System.Threading.Thread.Sleep(500);
            ulong livreDepois = Status().ullAvailPhys;

            BeginInvoke((Action)(() =>
            {
                long liberado = (long)livreDepois - (long)livreAntes;
                lblStatus.Text = string.Format("Feito! Liberado: {0}\n({1} processos aparados, caches e standby limpos)",
                    liberado > 0 ? GB((ulong)liberado) : "0 GB", processos);
                btn.Text = "LIMPAR RAM";
                btn.Enabled = true;
                AtualizarUso();
            }));
        });
    }

    static int ExecutarLimpeza()
    {
        AtivarPrivilegio("SeDebugPrivilege");
        AtivarPrivilegio("SeIncreaseQuotaPrivilege");
        AtivarPrivilegio("SeProfileSingleProcessPrivilege");

        // 1) Apara a memória de todos os processos (vai pro arquivo de paginação se precisar)
        int ok = 0;
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                IntPtr h = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION | PROCESS_SET_QUOTA, false, p.Id);
                if (h != IntPtr.Zero)
                {
                    if (EmptyWorkingSet(h)) ok++;
                    CloseHandle(h);
                }
            }
            catch { }
            finally { p.Dispose(); }
        }

        // 2) Limpa o cache de arquivos do sistema
        try { SetSystemFileCacheSize(new IntPtr(-1), new IntPtr(-1), 0); } catch { }

        // 3) Grava a lista modificada no disco e 4) esvazia a lista standby
        try
        {
            int cmd = MemoryFlushModifiedList;
            NtSetSystemInformation(SystemMemoryListInformation, ref cmd, sizeof(int));
            cmd = MemoryPurgeStandbyList;
            NtSetSystemInformation(SystemMemoryListInformation, ref cmd, sizeof(int));
        }
        catch { }

        return ok;
    }

    static void AtivarPrivilegio(string nome)
    {
        IntPtr token;
        if (!OpenProcessToken(Process.GetCurrentProcess().Handle, 0x0020 | 0x0008, out token)) return;
        var tp = new TOKEN_PRIVILEGES { Count = 1, Attr = 0x2 };
        if (LookupPrivilegeValue(null, nome, out tp.Luid))
            AdjustTokenPrivileges(token, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
        CloseHandle(token);
    }

    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.Run(new LimpaRAM());
    }
}
