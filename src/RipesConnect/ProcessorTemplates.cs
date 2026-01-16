namespace RipesConnect;

public static class ProcessorTemplates
{
    // =============================================================
    // 5-STAGE PIPELINE CODE
    // =============================================================
    public const string FiveStageCode = @"
`timescale 1ns / 1ps

module processor_top (
    input  wire CLK,
    input  wire RESET_BTN,
    output wire LED0,
    output wire LED1,
    output wire LED2,
    output wire LED3,
    output wire LED4,
    output wire LED5
);

  parameter MEM_FILE = ""../code.mem""; // Quotes escaped for C#

  reg [31:0] counter;
  always @(posedge CLK) counter <= counter + 1;
  wire slow_clk = counter[24];
  wire cpu_reset = ~RESET_BTN;

  wire [31:0] debug_pc;
  wire [31:0] debug_x10;

  riscv_pipeline_core #(
      .MEM_FILE(MEM_FILE)
  ) cpu (
      .clk(slow_clk),
      .reset(cpu_reset),
      .debug_pc(debug_pc),
      .debug_x10(debug_x10)
  );

  assign LED5 = slow_clk;
  assign LED4 = debug_x10[1];
  assign LED3 = debug_x10[0];
  assign LED2 = debug_pc[4];
  assign LED1 = debug_pc[3];
  assign LED0 = debug_pc[2];

endmodule


module riscv_pipeline_core #(
    parameter MEM_FILE = ""code.mem""
) (
    input  wire        clk,
    input  wire        reset,
    output wire [31:0] debug_pc,
    output wire [31:0] debug_x10
);

  reg  [31:0] pc;
  wire [31:0] pc_plus_4;
  wire [31:0] if_instr_raw;
  wire [31:0] if_instr_decoded;

  wire        ex_branch_taken;
  wire [31:0] ex_branch_target;

  assign pc_plus_4 = pc + 32'h4;

  wire [31:0] pc_next = (ex_branch_taken) ? ex_branch_target : pc_plus_4;

  always @(posedge clk or posedge reset) begin
    if (reset) pc <= 32'h0000_0000;
    else pc <= pc_next;
  end

  reg [7:0] imem[0:4095];
  initial $readmemh(MEM_FILE, imem);

  assign if_instr_raw = {imem[pc+3], imem[pc+2], imem[pc+1], imem[pc]};

  assign if_instr_decoded = if_instr_raw;


  reg  [31:0] if_id_pc;
  reg  [31:0] if_id_instr;
  wire        enable_ifid = 1'b1;
  wire        clear_ifid = ex_branch_taken;

  always @(posedge clk or posedge reset) begin
    if (reset) begin
      if_id_pc    <= 32'b0;
      if_id_instr <= 32'b0;
    end else if (clear_ifid) begin
      if_id_pc    <= 32'b0;
      if_id_instr <= 32'b0;
    end else if (enable_ifid) begin
      if_id_pc    <= pc;
      if_id_instr <= if_instr_decoded;
    end
  end


  wire [31:0] id_instr = if_id_instr;
  wire [31:0] id_pc    = if_id_pc;

  wire [4:0] id_rs1 = id_instr[19:15];
  wire [4:0] id_rs2 = id_instr[24:20];
  wire [4:0] id_rd  = id_instr[11:7];

  reg [31:0] regs [0:31];
  integer i;
  initial for (i = 0; i < 32; i = i + 1) regs[i] = 32'b0;

  wire        wb_reg_write;
  wire [ 4:0] wb_rd;
  wire [31:0] wb_write_data;

  always @(posedge clk) begin
    if (wb_reg_write && wb_rd != 5'b0) regs[wb_rd] <= wb_write_data;
  end

  wire [31:0] id_rdata1 = regs[id_rs1];
  wire [31:0] id_rdata2 = regs[id_rs2];
  assign debug_x10 = regs[10];

  reg [31:0] id_imm;
  always @(*) begin
    case (id_instr[6:0])
      7'b0010011, 7'b0000011, 7'b1100111:
      id_imm = {{20{id_instr[31]}}, id_instr[31:20]};
      7'b0100011:
      id_imm = {{20{id_instr[31]}}, id_instr[31:25], id_instr[11:7]};
      7'b1100011:
      id_imm = {
        {19{id_instr[31]}}, id_instr[31], id_instr[7], id_instr[30:25], id_instr[11:8], 1'b0
      };
      7'b0010111, 7'b0110111:
      id_imm = {id_instr[31:12], 12'b0};
      7'b1101111:
      id_imm = {
        {11{id_instr[31]}}, id_instr[31], id_instr[19:12], id_instr[20], id_instr[30:21], 1'b0
      };
      default: id_imm = 32'b0;
    endcase
  end

  reg        id_reg_write;
  reg        id_mem_read;
  reg        id_mem_write;
  reg        id_mem_to_reg;
  reg        id_alu_src;
  reg        id_branch;
  reg        id_jal_jump;
  reg [3:0] id_alu_op;
  reg [2:0] id_alu_func;

  always @(*) begin
    id_reg_write  = 1'b0;
    id_mem_read   = 1'b0;
    id_mem_write  = 1'b0;
    id_mem_to_reg = 1'b0;
    id_alu_src    = 1'b0;
    id_branch     = 1'b0;
    id_jal_jump   = 1'b0;
    id_alu_op     = 4'b0000;
    id_alu_func   = 3'b000;

    case (id_instr[6:0])
      7'b0110011: begin
        id_reg_write = 1'b1;
        id_alu_op = 4'b0000;
        id_alu_func = id_instr[14:12];
      end
      7'b0010011: begin
        id_reg_write = 1'b1;
        id_alu_src = 1'b1;
        id_alu_op = 4'b0000;
        id_alu_func = id_instr[14:12];
      end
      7'b0000011: begin
        id_reg_write = 1'b1;
        id_mem_read = 1'b1;
        id_mem_to_reg = 1'b1;
        id_alu_src = 1'b1;
        id_alu_op = 4'b0000;
      end
      7'b0100011: begin
        id_mem_write = 1'b1;
        id_alu_src = 1'b1;
        id_alu_op = 4'b0000;
      end
      7'b1100011: begin
        id_branch    = 1'b1;
        id_alu_op    = 4'b0001;
        id_alu_func = id_instr[14:12];
      end
      7'b1101111: begin
        id_reg_write = 1'b1;
        id_jal_jump = 1'b1;
        id_alu_op = 4'b0010;
      end
      7'b1100111: begin
        id_reg_write = 1'b1;
        id_jal_jump = 1'b1;
        id_alu_src = 1'b1;
        id_alu_op = 4'b0011;
      end
      7'b0010111: begin
        id_reg_write = 1'b1;
        id_alu_src = 1'b1;
        id_alu_op = 4'b0100;
      end
      7'b0110111: begin
        id_reg_write = 1'b1;
        id_alu_src = 1'b1;
        id_alu_op = 4'b0101;
      end
      default: begin
      end
    endcase
  end


  reg [31:0] id_ex_pc, id_ex_rdata1, id_ex_rdata2, id_ex_imm;
  reg [ 4:0] id_ex_rd;
  reg [31:0] id_ex_instr;
  reg id_ex_reg_write, id_ex_mem_read, id_ex_mem_write, id_ex_mem_to_reg;
  reg id_ex_alu_src, id_ex_branch, id_ex_jal_jump;
  reg [3:0] id_ex_alu_op;
  reg [2:0] id_ex_alu_func;

  wire enable_idex = 1'b1;
  wire clear_idex = ex_branch_taken;

  always @(posedge clk or posedge reset) begin
    if (reset) begin
      id_ex_pc             <= 32'b0;
      id_ex_rdata1         <= 32'b0;
      id_ex_rdata2         <= 32'b0;
      id_ex_imm            <= 32'b0;
      id_ex_rd             <= 5'b0;
      id_ex_instr          <= 32'b0;
      id_ex_reg_write      <= 1'b0;
      id_ex_mem_read       <= 1'b0;
      id_ex_mem_write      <= 1'b0;
      id_ex_mem_to_reg     <= 1'b0;
      id_ex_alu_src        <= 1'b0;
      id_ex_branch         <= 1'b0;
      id_ex_jal_jump       <= 1'b0;
      id_ex_alu_op         <= 4'b0;
      id_ex_alu_func       <= 3'b0;
    end else if (clear_idex) begin
      id_ex_pc             <= 32'b0;
      id_ex_rdata1         <= 32'b0;
      id_ex_rdata2         <= 32'b0;
      id_ex_imm            <= 32'b0;
      id_ex_rd             <= 5'b0;
      id_ex_instr          <= 32'b0;
      id_ex_reg_write      <= 1'b0;
      id_ex_mem_read       <= 1'b0;
      id_ex_mem_write      <= 1'b0;
      id_ex_mem_to_reg     <= 1'b0;
      id_ex_alu_src        <= 1'b0;
      id_ex_branch         <= 1'b0;
      id_ex_jal_jump       <= 1'b0;
      id_ex_alu_op         <= 4'b0;
      id_ex_alu_func       <= 3'b0;
    end else if (enable_idex) begin
      id_ex_pc             <= id_pc;
      id_ex_rdata1         <= id_rdata1;
      id_ex_rdata2         <= id_rdata2;
      id_ex_imm            <= id_imm;
      id_ex_rd             <= id_rd;
      id_ex_instr          <= id_instr;
      id_ex_reg_write      <= id_reg_write;
      id_ex_mem_read       <= id_mem_read;
      id_ex_mem_write      <= id_mem_write;
      id_ex_mem_to_reg     <= id_mem_to_reg;
      id_ex_alu_src        <= id_alu_src;
      id_ex_branch         <= id_branch;
      id_ex_jal_jump       <= id_jal_jump;
      id_ex_alu_op         <= id_alu_op;
      id_ex_alu_func       <= id_alu_func;
    end
  end


  wire [31:0] ex_op1;
  wire [31:0] ex_op2;
  wire [31:0] ex_alu_res;

  wire is_auipc = (id_ex_alu_op == 4'b0100);
  assign ex_op1 = (is_auipc) ? id_ex_pc : id_ex_rdata1;

  assign ex_op2 = (id_ex_alu_src) ? id_ex_imm : id_ex_rdata2;

  reg [31:0] alu_result;
  always @(*) begin
    case (id_ex_alu_op)
      4'b0000: begin
        case (id_ex_alu_func)
          3'b000:  alu_result = ex_op1 + ex_op2;
          3'b001:  alu_result = ex_op1 << ex_op2[4:0];
          3'b010:  alu_result = ($signed(ex_op1) < $signed(ex_op2)) ? 32'd1 : 32'd0;
          3'b011:  alu_result = (ex_op1 < ex_op2) ? 32'd1 : 32'd0;
          3'b100:  alu_result = ex_op1 ^ ex_op2;
          3'b101: begin
            if (id_ex_instr[30] == 1'b0) alu_result = ex_op1 >> ex_op2[4:0];
            else alu_result = $signed(ex_op1) >>> ex_op2[4:0];
          end
          3'b110:  alu_result = ex_op1 | ex_op2;
          3'b111:  alu_result = ex_op1 & ex_op2;
          default: alu_result = ex_op1 + ex_op2;
        endcase
      end
      4'b0001: alu_result = ex_op1 - ex_op2;
      4'b0010: alu_result = id_ex_pc + 32'h4;
      4'b0011: alu_result = id_ex_rdata1 + id_ex_imm;
      4'b0100: alu_result = id_ex_pc + id_ex_imm;
      4'b0101: alu_result = id_ex_imm;
      default: alu_result = 32'b0;
    endcase
  end
  assign ex_alu_res = alu_result;

  wire ex_zero = (ex_alu_res == 32'b0);
  wire ex_less_signed = ($signed(id_ex_rdata1) < $signed(id_ex_rdata2));
  wire ex_less_unsigned = (id_ex_rdata1 < id_ex_rdata2);

  wire [31:0] ex_calc_branch_target = (id_ex_alu_op == 4'b0011) ? (alu_result & ~32'h1)
  : (id_ex_pc + id_ex_imm);

  assign ex_branch_target = ex_calc_branch_target;

  reg branch_condition_met;
  always @(*) begin
    branch_condition_met = 1'b0;
    case (id_ex_alu_func)
      3'b000:  branch_condition_met = ex_zero;
      3'b001:  branch_condition_met = ~ex_zero;
      3'b100:  branch_condition_met = ex_less_signed;
      3'b101:  branch_condition_met = ~ex_less_signed;
      3'b110:  branch_condition_met = ex_less_unsigned;
      3'b111:  branch_condition_met = ~ex_less_unsigned;
      default: branch_condition_met = 1'b0;
    endcase
  end

  assign ex_branch_taken = (id_ex_branch & branch_condition_met) | id_ex_jal_jump;


  reg [31:0] ex_mem_alu_res;
  reg [31:0] ex_mem_wdata;
  reg [ 4:0] ex_mem_rd;
  reg ex_mem_reg_write, ex_mem_mem_read, ex_mem_mem_write, ex_mem_mem_to_reg;

  always @(posedge clk or posedge reset) begin
    if (reset) begin
      ex_mem_alu_res    <= 32'b0;
      ex_mem_wdata      <= 32'b0;
      ex_mem_rd         <= 5'b0;
      ex_mem_reg_write  <= 1'b0;
      ex_mem_mem_read   <= 1'b0;
      ex_mem_mem_write  <= 1'b0;
      ex_mem_mem_to_reg <= 1'b0;
    end else begin
      ex_mem_alu_res    <= ex_alu_res;
      ex_mem_wdata      <= id_ex_rdata2;
      ex_mem_rd         <= id_ex_rd;
      ex_mem_reg_write  <= id_ex_reg_write;
      ex_mem_mem_read   <= id_ex_mem_read;
      ex_mem_mem_write  <= id_ex_mem_write;
      ex_mem_mem_to_reg <= id_ex_mem_to_reg;
    end
  end


  reg [31:0] dmem[0:255];
  wire [31:0] mem_rdata;
  wire [7:0] mem_addr_idx = ex_mem_alu_res[9:2];

  always @(posedge clk) begin
    if (ex_mem_mem_write) dmem[mem_addr_idx] <= ex_mem_wdata;
  end

  assign mem_rdata = dmem[mem_addr_idx];

  reg [31:0] mem_wb_rdata;
  reg [31:0] mem_wb_alu_res;
  reg [ 4:0] mem_wb_rd;
  reg        mem_wb_reg_write;
  reg        mem_wb_mem_to_reg;

  always @(posedge clk or posedge reset) begin
    if (reset) begin
      mem_wb_rdata      <= 32'b0;
      mem_wb_alu_res    <= 32'b0;
      mem_wb_rd         <= 5'b0;
      mem_wb_reg_write  <= 1'b0;
      mem_wb_mem_to_reg <= 1'b0;
    end else begin
      mem_wb_rdata      <= mem_rdata;
      mem_wb_alu_res    <= ex_mem_alu_res;
      mem_wb_rd         <= ex_mem_rd;
      mem_wb_reg_write  <= ex_mem_reg_write;
      mem_wb_mem_to_reg <= ex_mem_mem_to_reg;
    end
  end


  assign wb_reg_write  = mem_wb_reg_write;
  assign wb_rd         = mem_wb_rd;
  assign wb_write_data = (mem_wb_mem_to_reg) ? mem_wb_rdata : mem_wb_alu_res;

  reg [31:0] if_pc_debug;
  always @(posedge clk) if_pc_debug <= pc;
  assign debug_pc = if_pc_debug;

endmodule
";

    // =============================================================
    // SINGLE CYCLE CODE
    // =============================================================
    public const string SingleCycleCode = @"
`timescale 1ns / 1ps

module processor_top (
    input  wire CLK,
    input  wire RESET_BTN,
    output wire LED0,
    output wire LED1,
    output wire LED2,
    output wire LED3,
    output wire LED4,
    output wire LED5
);
  parameter MEM_FILE = ""../code.mem""; // Quotes escaped for C#

  reg [31:0] counter;
  always @(posedge CLK) counter <= counter + 1;
  wire slow_clk = counter[24];
  wire cpu_reset = ~RESET_BTN;

  wire [31:0] debug_pc;
  wire [31:0] debug_x10;

  riscv_single_cycle_core #(
      .MEM_FILE(MEM_FILE)
  ) cpu (
      .clk(slow_clk),
      .reset(cpu_reset),
      .debug_pc(debug_pc),
      .debug_x10(debug_x10)
  );

  assign LED5 = slow_clk;
  assign LED4 = debug_x10[1];
  assign LED3 = debug_x10[0];
  assign LED2 = debug_pc[4];
  assign LED1 = debug_pc[3];
  assign LED0 = debug_pc[2];

endmodule


module riscv_single_cycle_core #(
    parameter MEM_FILE = ""code.mem""
) (
    input  wire        clk,
    input  wire        reset,
    output wire [31:0] debug_pc,
    output wire [31:0] debug_x10
);

  reg  [31:0] pc;
  wire [31:0] pc_next;
  wire [31:0] pc_plus_4;
  wire [31:0] branch_target;  

  wire        pc_src_branch;
  wire        pc_src_jal;
  wire        pc_src_jalr;
  wire        branch_taken;  

  assign pc_plus_4 = pc + 32'h4;

  reg [7:0] imem[0:4095];
  initial $readmemh(MEM_FILE, imem);

  wire [31:0] instr;
  assign instr = {imem[pc+3], imem[pc+2], imem[pc+1], imem[pc]};

  always @(posedge clk or posedge reset) begin
    if (reset) pc <= 32'h0000_0000;
    else pc <= pc_next;
  end

  wire    [ 6:0] opcode = instr[6:0];
  wire    [ 4:0] rd = instr[11:7];
  wire    [ 2:0] funct3 = instr[14:12];
  wire    [ 4:0] rs1 = instr[19:15];
  wire    [ 4:0] rs2 = instr[24:20];
  wire    [ 6:0] funct7 = instr[31:25];

  reg     [31:0] regs                  [0:31];
  integer        i;
  initial for (i = 0; i < 32; i = i + 1) regs[i] = 32'b0;

  wire [31:0] rdata1 = regs[rs1];
  wire [31:0] rdata2 = regs[rs2];

  assign debug_x10 = regs[10];

  reg [31:0] imm;
  always @(*) begin
    case (opcode)
      7'b0010011, 7'b0000011, 7'b1100111: imm = {{20{instr[31]}}, instr[31:20]};  
      7'b0100011: imm = {{20{instr[31]}}, instr[31:25], instr[11:7]};  
      7'b1100011:
      imm = {{19{instr[31]}}, instr[31], instr[7], instr[30:25], instr[11:8], 1'b0};  
      7'b0010111, 7'b0110111: imm = {instr[31:12], 12'b0};  
      7'b1101111:
      imm = {{11{instr[31]}}, instr[31], instr[19:12], instr[20], instr[30:21], 1'b0};  
      default: imm = 32'b0;
    endcase
  end

  reg        ctrl_branch;  
  reg        ctrl_mem_read;
  reg        ctrl_mem_to_reg;  
  reg [1:0] ctrl_wb_mux_sel;  
  reg        ctrl_mem_write;
  reg        ctrl_alu_src_op1;  
  reg        ctrl_alu_src_op2;  
  reg        ctrl_reg_write;
  reg        ctrl_jump_jal;  
  reg        ctrl_jump_jalr;  
  reg [3:0] ctrl_alu_op;  

  always @(*) begin
    ctrl_branch      = 0;
    ctrl_mem_read    = 0;
    ctrl_wb_mux_sel  = 0;  
    ctrl_mem_write   = 0;
    ctrl_alu_src_op1 = 0;  
    ctrl_alu_src_op2 = 0;  
    ctrl_reg_write   = 0;
    ctrl_jump_jal    = 0;
    ctrl_jump_jalr   = 0;
    ctrl_alu_op      = 4'b0000;

    case (opcode)
      7'b0110011: begin  
        ctrl_reg_write = 1;
        ctrl_alu_op    = 4'b0000;
      end
      7'b0010011: begin  
        ctrl_reg_write   = 1;
        ctrl_alu_src_op2 = 1;  
        ctrl_alu_op      = 4'b0000;
      end
      7'b0000011: begin  
        ctrl_reg_write   = 1;
        ctrl_alu_src_op2 = 1;  
        ctrl_mem_read    = 1;
        ctrl_wb_mux_sel  = 1;  
        ctrl_alu_op      = 4'b0000;  
      end
      7'b0100011: begin  
        ctrl_mem_write   = 1;
        ctrl_alu_src_op2 = 1;  
        ctrl_alu_op      = 4'b0000;  
      end
      7'b1100011: begin  
        ctrl_branch = 1;
        ctrl_alu_op = 4'b0001;  
      end
      7'b1101111: begin  
        ctrl_reg_write  = 1;
        ctrl_jump_jal   = 1;
        ctrl_wb_mux_sel = 2;  
      end
      7'b1100111: begin  
        ctrl_reg_write   = 1;
        ctrl_jump_jalr   = 1;
        ctrl_alu_src_op2 = 1;  
        ctrl_wb_mux_sel  = 2;  
        ctrl_alu_op      = 4'b0000;  
      end
      7'b0010111: begin  
        ctrl_reg_write   = 1;
        ctrl_alu_src_op1 = 1;  
        ctrl_alu_src_op2 = 1;  
        ctrl_alu_op      = 4'b0000;  
      end
      7'b0110111: begin  
        ctrl_reg_write   = 1;
        ctrl_alu_src_op2 = 1;
        ctrl_alu_op      = 4'b0101;  
      end
    endcase
  end

  reg branch_condition_met;

  wire signed [31:0] rdata1_s = rdata1;
  wire signed [31:0] rdata2_s = rdata2;

  always @(*) begin
    case (funct3)
      3'b000:  branch_condition_met = (rdata1 == rdata2);  
      3'b001:  branch_condition_met = (rdata1 != rdata2);  
      3'b100:  branch_condition_met = (rdata1_s < rdata2_s);  
      3'b101:  branch_condition_met = (rdata1_s >= rdata2_s);  
      3'b110:  branch_condition_met = (rdata1 < rdata2);  
      3'b111:  branch_condition_met = (rdata1 >= rdata2);  
      default: branch_condition_met = 0;
    endcase
  end

  assign branch_taken = ctrl_branch & branch_condition_met;

  wire [31:0] alu_op1 = (ctrl_alu_src_op1) ? pc : rdata1;

  wire [31:0] alu_op2 = (ctrl_alu_src_op2) ? imm : rdata2;

  reg  [31:0] alu_result;
  always @(*) begin
    case (ctrl_alu_op)
      4'b0000: begin  
        if (opcode == 7'b0110011 && funct7[5])  
          alu_result = alu_op1 - alu_op2;
        else if (opcode == 7'b0110011 || opcode == 7'b0010011) begin
          case (funct3)
            3'b000:
            alu_result = (opcode == 7'b0110011 && funct7[5]) ? (alu_op1 - alu_op2) : (alu_op1 + alu_op2); 
            3'b001: alu_result = alu_op1 << alu_op2[4:0];  
            3'b010: alu_result = ($signed(alu_op1) < $signed(alu_op2)) ? 1 : 0;  
            3'b011: alu_result = (alu_op1 < alu_op2) ? 1 : 0;  
            3'b100: alu_result = alu_op1 ^ alu_op2;  
            3'b101:
            alu_result = (funct7[5]) ?
                ($signed(alu_op1) >>> alu_op2[4:0]) : (alu_op1 >> alu_op2[4:0]);  
            3'b110: alu_result = alu_op1 | alu_op2;  
            3'b111: alu_result = alu_op1 & alu_op2;  
            default: alu_result = alu_op1 + alu_op2;
          endcase
        end else begin
          alu_result = alu_op1 + alu_op2;  
        end
      end
      4'b0101: alu_result = alu_op2;  
      default: alu_result = 0;
    endcase
  end

  reg [31:0] dmem[0:255];
  wire [31:0] mem_rdata;
  wire [7:0] mem_addr_idx = alu_result[9:2];  

  assign mem_rdata = dmem[mem_addr_idx];

  always @(posedge clk) begin
    if (ctrl_mem_write) dmem[mem_addr_idx] <= rdata2;  
  end

  reg [31:0] wb_data;
  always @(*) begin
    case (ctrl_wb_mux_sel)
      2'b00:   wb_data = alu_result;
      2'b01:   wb_data = mem_rdata;
      2'b10:   wb_data = pc_plus_4;
      default: wb_data = alu_result;
    endcase
  end

  always @(posedge clk) begin
    if (ctrl_reg_write && rd != 0) regs[rd] <= wb_data;
  end

  wire [31:0] pc_target_branch_jal = pc + imm;
  wire [31:0] pc_target_jalr = alu_result & ~32'd1;

  assign pc_next = (branch_taken || ctrl_jump_jal) ? pc_target_branch_jal :
                   (ctrl_jump_jalr)                 ? pc_target_jalr :
                                                      pc_plus_4;

  assign debug_pc = pc;

endmodule
";
}