library IEEE;
use IEEE.STD_LOGIC_1164.ALL;

entity quantum_cpr_logic is
    Port ( 
        -- == INPUT (Qubit Kontrol / Sensor) ==
        CLK      : in STD_LOGIC; -- Clock Sistem
        RESET    : in STD_LOGIC; -- Global Reset
        
        -- Sensor Utama (Control Qubits untuk FSM)
        H        : in STD_LOGIC; -- Sensor Tangan (Hand)
        C_DONE   : in STD_LOGIC; -- Counter Compress Selesai (30x)
        V_DONE   : in STD_LOGIC; -- Counter Ventilasi Selesai (2x)
        
        -- Sensor Tambahan (Untuk Aktuator Kombinasional)
        D        : in STD_LOGIC_VECTOR(1 downto 0); -- Depth (10=Baik)
        R        : in STD_LOGIC_VECTOR(1 downto 0); -- Rate (10=Baik)
        RC       : in STD_LOGIC;                    -- Recoil (1=Baik)
        P        : in STD_LOGIC_VECTOR(1 downto 0); -- Ritme (10=VF, 01=VT)
        AMSA_H   : in STD_LOGIC;                    -- AMSA High (1=Ya)

        -- == OUTPUT (Hasil Pengukuran Qubit) ==
        -- State Qubit (Memori FSM)
        S1_out   : out STD_LOGIC; -- Qubit State 1
        S0_out   : out STD_LOGIC; -- Qubit State 0
        
        -- Aktuator Output (Hasil Logika Gate Step 7)
        METRO    : out STD_LOGIC; -- Rate Metronome
        VENT_H   : out STD_LOGIC; -- Ventilhelm
        Q_SCORE  : out STD_LOGIC; -- Quality Score (Green LED)
        SHOCK    : out STD_LOGIC  -- Shock Alert
    );
end quantum_cpr_logic;

architecture Behavioral of quantum_cpr_logic is
    -- Register untuk menyimpan State Qubit (|S1 S0>)
    signal S1, S0 : STD_LOGIC := '0'; 

begin
    
    process(CLK, RESET)
    begin
        if RESET = '1' then
            -- Reset ke Ground State |00>
            S1 <= '0';
            S0 <= '0';
        elsif rising_edge(CLK) then
            
            -- Logika ini MENGGANTIKAN Flip-Flop Klasik.
            -- Ini adalah implementasi langsung dari Tabel Step 5.3
            
            if (S1 = '0' and S0 = '0') then      -- State |00> (IDLE)
                if H = '1' then
                    -- Operasi CNOT: Target S0 dibalik jika H=1
                    S0 <= '1'; 
                    S1 <= '0';
                else
                    -- Identity (I): Tidak berubah
                    S0 <= '0'; S1 <= '0';
                end if;

            elsif (S1 = '0' and S0 = '1') then   -- State |01> (COMPRESS)
                if C_DONE = '1' then
                    -- Operasi FREDKIN (SWAP): Tukar S1 dan S0
                    S1 <= '1'; -- S0 lama pindah ke S1
                    S0 <= '0'; -- S1 lama pindah ke S0
                else
                    -- Identity (I)
                    S1 <= '0'; S0 <= '1';
                end if;

            elsif (S1 = '1' and S0 = '0') then   -- State |10> (VENTILATE)
                if V_DONE = '1' then
                    -- Operasi FREDKIN (SWAP): Tukar S1 dan S0
                    S1 <= '0'; 
                    S0 <= '1';
                else
                    -- Identity (I)
                    S1 <= '1'; S0 <= '0';
                end if;

            elsif (S1 = '1' and S0 = '1') then   -- State |11> (UNUSED)
                -- Operasi PAULI-X Ganda (Reset)
                S1 <= '0'; 
                S0 <= '0';
            end if;
            
        end if;
    end process;

    -- Teruskan nilai state internal ke port output
    S1_out <= S1;
    S0_out <= S0;
    
    -- 1. Aktuator Q_SCORE (Logika Multi-Controlled Toffoli)
    -- Syarat: D=10, R=10, RC=1
    -- Di VHDL, Toffoli gate disimulasikan dengan AND
    Q_SCORE <= '1' when (D = "10" and R = "10" and RC = '1') else '0';

    -- 2. Aktuator SHOCK (Logika CNOT + Toffoli)
    -- Syarat: Ritme P1 beda dengan P0 (XOR/CNOT) DAN AMSA_H = 1
    SHOCK   <= '1' when ((P(1) xor P(0)) = '1' and AMSA_H = '1') else '0';

    -- 3. Aktuator Output Moore (Bergantung State)
    -- METRO nyala jika State = |01>
    METRO   <= '1' when (S1 = '0' and S0 = '1') else '0';
    
    -- VENT_H nyala jika State = |10>
    VENT_H  <= '1' when (S1 = '1' and S0 = '0') else '0';

end Behavioral;
