# Overenie kampane

Unity 6000.6.6f1, Linux, 15. 11. 2026.

- Kompilácia projektu: úspešná; starší kód má upozornenia na zastarané Unity API.
- Modelové kontroly: úspešné. Päť levelov, výpočet skóre a časových bonusov, penalizácie, opakovanie, uloženie postupu a ochrana pred duplicitnou odmenou.
- Automatizovaný Play Mode bez grafického okna: úspešný (CAMPAIGN RUNTIME PASS). Štyri interiéry, vytvorená navigačná sieť, cesty k miestnostiam a prístup ku všetkým cieľom, odomykanie dverí, prechod piatimi levelmi, pauza výsledkov, zastavenie hry na finálnom skóre a uloženie rekordu. Počas meranej časti neboli zaznamenané spravované chyby ani výnimky.
- Obsah JSON: 5 levelov, 22 krokov, 32 dostupných rozhovorov; kontrola pôvodných 157 dialógových kľúčov úspešná.

Test priechodu levelmi posiela udalosti priamo riadiacej logike. Nenahrádza ručné odohranie, kontrolu vizuálu, kamery, pocitu z pohybu a obtiažnosti prenasledovania. Export samostatnej spustiteľnej hry nebol vykonaný: ZIP obsahuje Unity projekt.

Opakovanie automatizovaného testu: Unity batchmode, projekt tohto balíka, executeMethod CampaignRuntimeChecks.Start (bez prepínača quit). Test dočasne zálohuje a po dokončení obnoví uložený postup a rekord.

## Ručné odskúšanie
1. Otvor OldQuarter a postupuj podľa cieľov od Mary.
2. Vyskúšaj prvú a tretiu osobu, vstup do všetkých štyroch interiérov a dvere pri prenasledovaní.
3. Skús nesprávne odpovede, vypršanie času a chytenie políciou; skontroluj penalizácie.
4. Dokonči kampaň. Over tabuľku výsledkov, novú kampaň a ukončenie Play Mode.
