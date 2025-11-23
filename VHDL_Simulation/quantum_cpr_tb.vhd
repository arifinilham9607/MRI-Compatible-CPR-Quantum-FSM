----------------------------------------------------------------------------------
-- File: quantum_cpr_tb.vhd
-- Deskripsi: Testbench untuk memverifikasi Matrix Transisi Kuantum
----------------------------------------------------------------------------------
library IEEE;
use IEEE.STD_LOGIC_1164.ALL;

entity quantum_cpr_tb is
-- Testbench tidak memiliki port
end quantum_cpr_tb;

architecture Behavioral of quantum_cpr_tb is

    -- 1. Panggil Unit yang akan dites (UUT)
    component quantum_cpr_logic
    Port ( 
        CLK      : in STD_LOGIC;
        RESET    : in STD_LOGIC;
        H        : in STD_LOGIC;
        C_DONE   : in STD_LOGIC;
        V_DONE   : in STD_LOGIC;
        D        : in STD_LOGIC_VECTOR(1 downto 0);
        R        : in STD_LOGIC_VECTOR(1 downto 0);
        RC       : in STD_LOGIC;
        P        : in STD_LOGIC_VECTOR(1 downto 0);
        AMSA_H   : in STD_LOGIC;
        S1_out   : out STD_LOGIC;
        S0_out   : out STD_LOGIC;
        METRO    : out STD_LOGIC;
        VENT_H   : out STD_LOGIC;
        Q_SCORE  : out STD_LOGIC;
        SHOCK    : out STD_LOGIC
    );
    end component;

    -- 2. Sinyal Kabel Virtual
    signal CLK, RESET, H, C_DONE, V_DONE, RC, AMSA_H : STD_LOGIC := '0';
    signal D, R, P : STD_LOGIC_VECTOR(1 downto 0) := "00";
    signal S1_out, S0_out, METRO, VENT_H, Q_SCORE, SHOCK : STD_LOGIC;
    
    -- Clock Period 10 ns
    constant clk_period : time := 10 ns;

begin

    -- 3. Hubungkan UUT
    uut: quantum_cpr_logic Port Map (
        CLK => CLK, RESET => RESET, H => H, 
        C_DONE => C_DONE, V_DONE => V_DONE,
        D => D, R => R, RC => RC, P => P, AMSA_H => AMSA_H,
        S1_out => S1_out, S0_out => S0_out,
        METRO => METRO, VENT_H => VENT_H, Q_SCORE => Q_SCORE, SHOCK => SHOCK
    );

    -- 4. Generator Clock
    clk_process :process
    begin
        CLK <= '0';
        wait for clk_period/2;
        CLK <= '1';
        wait for clk_period/2;
    end process;

    -- 5. Skenario Simulasi (Sesuai Tabel Step 5.3)
    stim_proc: process
    begin		
        -- A. Inisialisasi (Reset)
        RESET <= '1';
        wait for 20 ns;
        RESET <= '0';
        wait for 10 ns; -- State sekarang harus |00> (IDLE)

        -- B. Tes Transisi 1: IDLE -> COMPRESS
        -- Beri Input H=1 (Tangan menekan)
        H <= '1';
        wait for 20 ns; -- Tunggu clock. State harus berubah jadi |01>
        H <= '0';       -- Lepas tangan
        
        -- C. Tes Transisi 2: COMPRESS -> VENTILATE (SWAP)
        -- Beri Input C_DONE=1 (Kompresi selesai 30x)
        wait for 20 ns;
        C_DONE <= '1';
        wait for 20 ns; -- Tunggu clock. State harus SWAP jadi |10>
        C_DONE <= '0';
        
        -- D. Tes Transisi 3: VENTILATE -> COMPRESS (SWAP BALIK)
        -- Beri Input V_DONE=1 (Napas selesai 2x)
        wait for 20 ns;
        V_DONE <= '1';
        wait for 20 ns; -- Tunggu clock. State harus SWAP balik jadi |01>
        V_DONE <= '0';
        
        -- E. Tes Aktuator SHOCK (Paralel)
        -- Beri Ritme VF (10) dan AMSA Tinggi (1)
        P <= "10";
        AMSA_H <= '1';
        wait for 20 ns; -- SHOCK harus bernilai 1
        P <= "00";
        AMSA_H <= '0';

        wait;
    end process;

end Behavioral;