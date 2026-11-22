# hs_unity – päťdielna kampaň

## Spustenie
Rozbaľ celý ZIP, pridaj priečinok do Unity Hub a otvor v Unity 6000.6.6f1. Otvor Assets/Scenes/OldQuarter.unity a stlač Play. Pri prvom otvorení počkaj na import balíkov.

## Ovládanie
WASD pohyb, myš rozhľad, Shift beh, medzerník skok, V prvá/tretia osoba. E otvára dvere a interakcie. V rozhovore klikni na odpoveď alebo použi 1–3. Escape otvorí pauzu. Medzi levelmi pokračuj tlačidlom alebo Enterom.

V safehouse vyzdvihni ingrediencie a plodinu, pri MIXING STATION stlač E a vyber produkt + ingredienciu. Miešanie je možné reťaziť. Pri PACKING stanici vyber surový produkt alebo maj v inventári ľubovoľný surový produkt a stlač E; potrebuješ Empty Bag.

## Kampaň
Päť postupne odomykaných levelov sa odohráva v jednej rozšírenej štvrti:
1. Zásielka: prevezmi zákazku od Mary, nájdi balík a odnes ho Tomášovi do kaviarne.
2. Výpadok prúdu: nájdi poistku a poznámku v dielni, oprav rozvádzač v správnom poradí.
3. Účtovná kniha: vyber si tichý postup s Adiným kódom alebo rýchle vylomenie skrinky s poplachom. Získaj knihu a ukry sa v dielni pred políciou. Zelená plocha úkrytu je v prístupnej stredovej uličke pred zásobovacou policou.
4. Dôkazy: porovnaj dokument zo skladu, faktúru z kaviarne a výpoveď Ady.
5. Posledné vysielanie: získaj prístup do rádiovej miestnosti, odošli správu a unikni na miesto vyzdvihnutia.

Sleduj aktuálny cieľ v HUD. Interiéry kaviarne, dielne, skladu a rádiovej miestnosti majú zariadenie, kolízie a otvárateľné dvere. Niektoré sa sprístupnia až v príslušnom leveli. Pri úkryte a úniku musíš zostať na mieste požadovaný čas bez aktívneho prenasledovania. Nesprávne odpovede a opakované pokusy znižujú skóre. Pri vypršaní času sa opakuje aktuálny level.

Výrobný prototyp obsahuje tri pestovateľné línie (Green Bud, Mushrooms, Coca Leaves), štyri ingrediencie s efektmi a tri kvality produktu (Standard, Premium, Superior). Kvalita sa určuje pri zavlažení podľa starostlivosti a ovplyvňuje trhovú cenu.

## Výsledok a koniec
Po každom leveli sa zobrazí výsledok. Skóre = základ misie + 2 body za každú zostávajúcu sekundu + 300 za misiu bez poplachu − 200 za opakovanie − 75 za nesprávnu odpoveď; minimum je nula. Poplach sa v rámci opakovaného levelu pamätá.
Po piatom leveli sa hranie zastaví a zobrazí sa konečné skóre, hodnotenie S/A/B/C, časy, chyby a najlepší výsledok. Môžeš pokračovať vo voľnom režime, začať novú kampaň alebo skončiť. Voľný režim zachováva rozhovory, inventár aj výrobu. Nová kampaň je dostupná aj v pauze. V editore Skončiť ukončí Play Mode. Najlepší výsledok ostáva uložený. Medzi dokončenými levelmi sa ukladá postup; rozpracovaný level sa po opätovnom spustení začína od začiatku. Dokončený uložený postup obnoví výsledky bez ďalšej odmeny.

## Lokálna analytika
JSONL záznamy sa ukladajú automaticky a ostávajú v počítači. V editore otvor **HighStakes > Analytics > Gameplay Sessions**, stlač **Reload** a vyber reláciu pre zobrazenie pohybu, zlyhaní a policajných odhalení na pôdoryse štvrte. Súhrny obsahujú misie, kroky, rozhodnutia, skóre a opakované pokusy. Vo výsledkoch je stručná efektivita poslednej misie, čas presunu a počet odhalení. Podrobná schéma a význam metrík sú v [TELEMETRY.md](TELEMETRY.md). Nejde o dotazník ani online službu.

## Rozsah balíka
Ide o samostatný Unity projekt s funkčnou prototypovou mapou a jednoduchými vlastnými modelmi. Neobsahuje chýbajúce Git LFS modely a textúry pôvodného repozitára. Päť levelov sú kapitoly v jednej mape, nie päť samostatných máp. Dialógy a misie sú v Assets/Resources/City/old_quarter_story.json.

Overenie a jeho obmedzenia sú v CAMPAIGN_VALIDATION.md.
