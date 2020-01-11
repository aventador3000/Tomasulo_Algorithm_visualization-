using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TomasuloSimulator
{
    public partial class Form1 : Form
    {
        private State curr_state;
        //инициализация формы
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void dataGridView1_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {

        }

        private void textBox2_TextChanged(object sender, EventArgs e)
        {

        }

        private void groupBox5_Enter(object sender, EventArgs e)
        {

        }

        private List<Instruction> Parse(String code)
        {
            List<Instruction> Ins = new List<Instruction>();
            foreach (String line in code.Split('\n'))
            {
                if (line.Length == 0 || line[0] == ';')
                    continue;
                String[] spans = line.Split();
                Instruction ins = new Instruction();
                switch (spans[0].ToUpper())
                {
                    case "L.D":
                        ins.op = OpType.LD;
                        break;
                    case "ADD.D":
                        ins.op = OpType.ADD;
                        break;
                    case "SUB.D":
                        ins.op = OpType.SUB;
                        break;
                    case "MULT.D":
                        ins.op = OpType.MUL;
                        break;
                    case "DIV.D":
                        ins.op = OpType.DIV;
                        break;
                    case "BNEZ":
                        ins.op = OpType.BNEZ;
                        break;
                    default:
                        throw new Exception("Неверная инструкция");
                }
                if (ins.op != OpType.BNEZ)
                {
                    ins.o1 = int.Parse(spans[1].Substring(1));
                    if (ins.op == OpType.LD)
                        ins.o2 = int.Parse(spans[2]);
                    else
                        ins.o2 = int.Parse(spans[2].Substring(1));
                    ins.o3 = int.Parse(spans[3].Substring(1));
                }
                else
                {
                    ins.o2 = int.Parse(spans[1].Substring(1));
                }
                Ins.Add(ins);
            }
            return Ins;
        }

        private String OpName(OpType op)
        {
            switch (op)
            {
                case OpType.LD:
                    return "L.D";
                case OpType.ADD:
                    return "ADD.D";
                case OpType.SUB:
                    return "SUB.D";
                case OpType.MUL:
                    return "MULT.D";
                case OpType.DIV:
                    return "DIV.D";
                case OpType.BNEZ:
                    return "BNEZ";
            }
            return "";
        }
        /*Этот метод проверяет состояние РС во время выполнения программы.*/
        private void ShowState(State s)
        {
            numericUpDown5.Value = CPU.cycle;
            dataGridView1.Rows.Clear();
            foreach (Instruction ins in s.Ins)
            {
                dataGridView1.Rows.Add(ins.ToString(),
                    ins.issue_time > 0 ? ins.issue_time.ToString() : "",
                    ins.exec_start_time > 0 ? ins.exec_start_time.ToString() : "",
                    ins.exec_end_time > 0 ? ins.exec_end_time.ToString() : "",
                    ins.wb_time > 0 ? ins.wb_time.ToString() : "");
            }
            dataGridView3.Rows.Clear();
            foreach (LoadUnit lu in s.LU)
            {
                if (lu.busy)
                    dataGridView3.Rows.Add(lu.time == 0 ? "" : lu.time.ToString(), lu.name, "Yes", lu.address, lu.result == null ? "" : lu.result.v);
                else
                    dataGridView3.Rows.Add("", lu.name, "No", "", "");
            }
            dataGridView2.Rows.Clear();
            foreach (ReserveStation rs in s.RS)
            {
                if (rs.busy)
                {
                    dataGridView2.Rows.Add(rs.time == 0 ? "" : rs.time.ToString(), rs.name, "Yes",
                        OpName(rs.ins.op), rs.Vj, rs.Vk, rs.Qj, rs.Qk);
                }
                else
                {
                    dataGridView2.Rows.Add("", rs.name, "No",
                        "", "", "", "", "");
                }
            }
            dataGridView4.Rows.Clear();
            dataGridView4.Rows.Add("Qi");
            dataGridView4.Rows.Add("Value");
            for (int i = 0; i < 16; i++)
            {
                if (s.Reg[i].Qi != null)
                    dataGridView4.Rows[0].Cells[i + 1].Value = s.Reg[i].Qi.name;
                if (s.Reg[i].value != null)
                    dataGridView4.Rows[1].Cells[i + 1].Value = s.Reg[i].value.ToString();
            }
            textBox2.Text = Value.text;
            label10.Text = s.BranchWaitingFor == null ? "" : "Инструкция перехода ожидает:" + s.BranchWaitingFor.ToString();
        }

        private bool prepare_data()
        {
            String code = textBox1.Text;
            try
            {
                CPU.load_time = (int)numericUpDown1.Value;
                CPU.add_time = (int)numericUpDown2.Value;
                CPU.mul_time = (int)numericUpDown3.Value;
                CPU.div_time = (int)numericUpDown4.Value;
                Value.text = "";
                Value.count = 0;
                curr_state = new State(Parse(code));
            }
            catch (Exception ee)
            {
                MessageBox.Show("Формат ввода неверена.");
                return false;
            }
            return true;
        }

        private void runto(int cycle)
        {
            textBox1.Enabled = false;
            numericUpDown1.Enabled = false;
            numericUpDown2.Enabled = false;
            numericUpDown3.Enabled = false;
            numericUpDown4.Enabled = false;
            if (cycle > 0)
            {
                if (cycle < CPU.cycle || CPU.cycle == 0)
                {
                    if (!prepare_data())
                    {
                        reset();
                        return;
                    }
                    CPU.cycle = 0;
                }
                while (cycle > CPU.cycle)
                {
                    CPU.cycle++;
                    curr_state.step();
                }
            }
            ShowState(curr_state);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            runto(CPU.cycle + 1);
        }

        private void label9_Click(object sender, EventArgs e)
        {

        }

        private void dataGridView2_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {

        }
        /*Этот метод обрабатывает сброс станций резервирования после завершения выполнения.*/

        private void reset()
        {
            textBox1.Enabled = true;
            numericUpDown1.Enabled = true;
            numericUpDown2.Enabled = true;
            numericUpDown3.Enabled = true;
            numericUpDown4.Enabled = true;
            CPU.cycle = 0;
            dataGridView1.Rows.Clear();
            dataGridView2.Rows.Clear();
            dataGridView3.Rows.Clear();
            dataGridView4.Rows.Clear();
            textBox2.Text = "";
            numericUpDown5.Value = 1;
            label10.Text = "";
        }

        private void button2_Click(object sender, EventArgs e)
        {
            reset();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            runto(CPU.cycle - 1);
        }

        private void numericUpDown5_ValueChanged(object sender, EventArgs e)
        {
            runto((int)numericUpDown5.Value);
        }

        private void groupBox4_Enter(object sender, EventArgs e)
        {

        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }
    }
}
