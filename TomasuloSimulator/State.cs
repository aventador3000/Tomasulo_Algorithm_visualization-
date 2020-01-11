using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms.VisualStyles;

namespace TomasuloSimulator
{
    class State
    {
        public List<ReserveStation> RS;
        public List<Register>   Reg;
        public List<LoadUnit> LU;
        public List<Instruction> Ins;
        public int NextToIssue;
        public bool branch;
        public Unit BranchWaitingFor;

        public State(List<Instruction> Ins)
        {
            RS = new List<ReserveStation>();
            RS.Add(new ReserveStation("Add1"));
            RS.Add(new ReserveStation("Add2"));
            RS.Add(new ReserveStation("Add3"));
            RS.Add(new ReserveStation("Mult1"));
            RS.Add(new ReserveStation("Mult2"));
            Reg = new List<Register>();
            for (int i = 0; i < 16; i++)
                Reg.Add(new Register(i * 2));
            LU = new List<LoadUnit>();
            LU.Add(new LoadUnit("Load1"));
            LU.Add(new LoadUnit("Load2"));
            LU.Add(new LoadUnit("Load3"));
            this.Ins = Ins;
        }

        private String symbol(OpType t)
        {
            switch (t)
            {
                case OpType.LD:
                    return "";
                case OpType.ADD:
                    return "+";
                case OpType.SUB:
                    return "-";
                case OpType.MUL:
                    return "*";
                case OpType.DIV:
                    return "/";
            }
            return "";
        }

        public void save_result(Unit u, Value v)
        {
            foreach (Register r in Reg)
            {
                if (Object.ReferenceEquals(r.Qi, u))
                {
                    r.Qi = null;
                    r.value = v;
                }
            }

            foreach (ReserveStation rs in RS)
            {
                if (Object.ReferenceEquals(rs.Qj, u))
                {
                    rs.Qj = null;
                    rs.Vj = v;
                }
                if (Object.ReferenceEquals(rs.Qk, u))
                {
                    rs.Qk = null;
                    rs.Vk = v;
                }
            }

            if (BranchWaitingFor != null)
            {
                if (Object.ReferenceEquals(BranchWaitingFor, u))
                {
                    BranchWaitingFor = null;
                }
            }
        }

        public void step()
        {
            foreach (LoadUnit lu in LU)
            {
                if (lu.busy)
                {
                    if (lu.ins.exec_start_time == 0)
                    {
                        lu.ins.exec_start_time = CPU.cycle;
                        lu.time = CPU.load_time - 1;
                        lu.address = String.Format("R[R{0}]+{1}", lu.ins.o3, lu.ins.o2);
                    }
                    else if (lu.ins.exec_end_time == 0)
                    {
                        lu.time--;
                        if (lu.time == 0)
                        {
                            lu.ins.exec_end_time = CPU.cycle;
                            lu.result = new Value("M[" + lu.address + "]");
                        }
                    }
                    else
                    {
                        lu.ins.wb_time = CPU.cycle;
                        lu.busy = false;
                        save_result(lu, lu.result);
                    }
                }
            }

            foreach (ReserveStation rs in RS)
            {
                if (rs.busy)
                {
                    if (rs.ins.exec_start_time == 0)
                    {
                        if (rs.ready) //ready
                        {
                            rs.ins.exec_start_time = CPU.cycle;
                            rs.time = (rs.ins.op == OpType.ADD || rs.ins.op == OpType.SUB ? CPU.add_time :
                                rs.ins.op == OpType.MUL ? CPU.mul_time : CPU.div_time) - 1;
                        }
                    }
                    else if (rs.ins.exec_end_time == 0)
                    {
                        rs.time--;
                        if (rs.time == 0)
                        {
                            rs.ins.exec_end_time = CPU.cycle;
                            rs.result = new Value(rs.Vj.ToString() + " " + symbol(rs.ins.op) + " " + rs.Vk.ToString());
                        }
                    }
                    else
                    {
                        rs.ins.wb_time = CPU.cycle;
                        rs.busy = false;
                        save_result(rs, rs.result);
                    }
                }
            }

            if (branch && BranchWaitingFor == null)
            {
                Ins[NextToIssue - 1].exec_start_time = CPU.cycle;
                Ins[NextToIssue - 1].exec_end_time = CPU.cycle;
                branch = false;
            }

            if (NextToIssue < Ins.Count && BranchWaitingFor == null)
            {
                switch (Ins[NextToIssue].op)
                {
                    case OpType.LD:
                        foreach (LoadUnit lu in LU)
                        {
                            if (!lu.busy)
                            {
                                Ins[NextToIssue].issue_time = CPU.cycle;
                                lu.ins = Ins[NextToIssue];
                                lu.busy = true;
                                lu.address = lu.ins.o2.ToString();
                                lu.result = null;
                                Reg[lu.ins.o1 / 2].Qi = lu;
                                NextToIssue++;
                                break;
                            }
                        }
                        break;
                    case OpType.BNEZ:
                        Ins[NextToIssue].issue_time = CPU.cycle;
                        branch = true;
                        BranchWaitingFor = Reg[Ins[NextToIssue].o2 / 2].Qi;
                        NextToIssue++;
                        break;
                    default:
                        int start, end;
                        if (Ins[NextToIssue].op == OpType.ADD || Ins[NextToIssue].op == OpType.SUB)
                        {
                            start = 0;
                            end = 3;
                        }
                        else
                        {
                            start = 3;
                            end = 5;
                        }
                        for (int i = start; i < end; i++)
                        {
                            ReserveStation rs = RS[i];
                            if (!rs.busy)
                            {
                                Ins[NextToIssue].issue_time = CPU.cycle;
                                rs.ins = Ins[NextToIssue];
                                rs.busy = true;
                                rs.ready = false;
                                if (Reg[rs.ins.o2 / 2].Qi != null)
                                {
                                    rs.Qj = Reg[rs.ins.o2 / 2].Qi;
                                    rs.Vj = null;
                                }
                                else
                                {
                                    rs.Qj = null;
                                    rs.Vj = Reg[rs.ins.o2 / 2].value;
                                }
                                if (Reg[rs.ins.o3 / 2].Qi != null)
                                {
                                    rs.Qk = Reg[rs.ins.o3 / 2].Qi;
                                    rs.Vk = null;
                                }
                                else
                                {
                                    rs.Qk = null;
                                    rs.Vk = Reg[rs.ins.o3 / 2].value;
                                }
                                Reg[rs.ins.o1 / 2].Qi = rs;
                                NextToIssue++;
                                break;
                            }
                        }
                        break;
                }
            }

            foreach (ReserveStation rs in RS)
            {
                if (rs.busy && rs.ins.exec_start_time == 0 && rs.Qj == null && rs.Qk == null)
                    rs.ready = true;
            }
        }
    }

    enum OpType
    {
        LD, ADD, SUB, MUL, DIV, BNEZ
    }

    class Value
    {
        public static int count = 0;
        public static String text = "";

        public String v;
        public int num;
        public bool R;

        public Value(String v)
        {
            count++;
            num = count;
            this.v = v;
            R = false;
            text += String.Format("M{0} = {1}", num, v) + Environment.NewLine;
        }

        public Value(int i)
        {
            R = true;
            v = "R[F" + i.ToString() + "]";
        }

        public override string ToString()
        {
            if (R)
                return v;
            else
                return "M" + num.ToString();
        }
    }

    class Unit
    {
        public int time;
        public String name;
        public bool busy;
        public Value result;
        public Instruction ins;

        public Unit(String name)
        {
            this.name = name;
        }

        public override string ToString()
        {
            return name;
        }
    }
    /*Этот метод обрабатывает обновление станций бронирования*/
    class ReserveStation : Unit
    {
        public Value Vj;
        public Value Vk;
        public Unit Qj;
        public Unit Qk;
        public bool ready;

        public ReserveStation(string name) : base(name)
        {
        }
    }

    class LoadUnit : Unit
    {
        public String address;

        public LoadUnit(string name) : base(name)
        {
        }
    }

    class Instruction
    {
        public OpType op;
        public int o1;
        public int o2;
        public int o3;
        public int issue_time;
        public int exec_start_time;
        public int exec_end_time;
        public int wb_time;
        /// <summary>
        /// /Выбор инструкции для исполнения
        /// </summary>
        /// <returns></returns>
        public override String ToString()
        {
            switch (op)
            {
                case OpType.LD:
                    return String.Format("L.D F{0},{1}(R{2})", o1, o2, o3);
                case OpType.ADD:
                    return String.Format("ADD.D F{0},F{1},F{2}", o1, o2, o3);
                case OpType.SUB:
                    return String.Format("SUB.D F{0},F{1},F{2}", o1, o2, o3);
                case OpType.MUL:
                    return String.Format("MULT.D F{0},F{1},F{2}", o1, o2, o3);
                case OpType.DIV:
                    return String.Format("DIV.D F{0},F{1},F{2}", o1, o2, o3);
                case OpType.BNEZ:
                    return String.Format("BNEZ F{0}", o2);
            }
            return "";
        }
    }
    /// <summary>
    /// Класс, работает с значением регистру соответствующему значению
    /// </summary>
    class Register
    {
        public Unit Qi;
        public Value value;

        public Register(int i)
        {
            value = new Value(i);
        }
    }
}
