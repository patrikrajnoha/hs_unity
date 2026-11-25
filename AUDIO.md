# Zvuk v HighStakes

Hudba a zvuky sa spúšťajú automaticky v menu a v OldQuarter. Projekt obsahuje 12 pôvodných syntetizovaných WAV súborov: pokojný hudobný podklad, hudbu prenasledovania, ruch mesta, dva druhy krokov a sedem zvukov rozhrania a herných udalostí. Nie sú potrebné externé balíky ani pripojenie počas hrania.

V **Nastavenia** v hlavnom menu alebo cez **Escape → Nastavenia** nájdeš štyri hlasitosti: celková, hudba, efekty a ruch mesta. Ovládanie funguje aj pri pozastavenej hre. Tlačidlá umožňujú stlmiť všetko a obnoviť predvolené zvukové nastavenia. Nastavenia sa ukladajú lokálne cez PlayerPrefs, nezávisle od kampane.

Pri prenasledovaní hudba plynulo prechádza do napätejšej slučky. Počas pauzy sa hudba stíši, ruch mesta a herné efekty pozastavia; zvuky rozhrania ostávajú použiteľné. Po skončení levelu zaznie výsledkový motív. Kroky sa riadia skutočne prejdenou vzdialenosťou na zemi a líšia sa v interiéri. Dvere používajú priestorový zvuk.

Zdrojové súbory sú v `Assets/Resources/Audio`. Znovu ich vytvoríš príkazom `python3 Tools/generate_audio.py`; generátor používa iba štandardné knižnice Pythonu. Implementácia je v `Assets/Scripts/Audio`. Ovládanie je preložené do slovenčiny a angličtiny.

Automatická kontrola `CampaignRuntimeChecks.Start` zahŕňa načítanie hudby, jedinú zvukovú službu, štyri posuvníky v pauze, nezávislé kanály, stlmenie, hranice hlasitosti a uloženie s obnovením nastavení. Test obnoví pôvodné používateľské nastavenia. Zvukový dojem treba navyše overiť pri normálnom hraní so zapnutým zvukom.

## Overenie 25. 11. 2026

Unity 6000.6.6f1 na Linuxe: kompilácia a automatický Play Mode test úspešné (návratový kód 0). Výstupy: `AUDIO RUNTIME PASS`, `CAMPAIGN MODEL PASS`, `CAMPAIGN FILE LOG PASS`, `FREE ROAM REGRESSION PASS`, `CAMPAIGN RUNTIME PASS`. Overené boli nastavenia zvuku, päťdielny postup, prístupnosť cieľov a miestností, dvere, výsledková obrazovka a voľný pohyb po kampani. Test bežal bez grafického okna; nenahrádza ručné posúdenie vzhľadu a počúvanie zvukového mixu.
