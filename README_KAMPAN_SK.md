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


## Samostatný sklad na voľné hranie

Výrobný sklad s označením WAREHOUSE je západne od dielne pri Mare a je prístupný od začiatku. Obsahuje pestovanie, vodu, náradie, balenie a miešanie. Pripravené produkty môžeš ponúkať viacerým obyvateľom na ulici; jednotliví obyvatelia majú vlastný čas do ďalšej ponuky a môžu kúpiť, odmietnuť alebo zavolať políciu. Nie je to misijná budova.

Päť pôvodných misií a všetky ich ciele zostávajú na pôvodných súradniciach. Pôvodná budova s účtovnou knihou a manifestom sa teraz volá ARCHIVE, aby sa odlíšila od skladu na voľnú výrobu. Poradie krokov, hádanky, odmeny a časové limity sa nemenia.

### Inventár a sprievodca skladom

- **Q** otvorí dosku inventára s 12 políčkami a uvoľní kurzor. Klik vyberie predmet; podržanie ľavého tlačidla a potiahnutie zmení poradie. Prázdne políčko presunie predmet na koniec zoznamu.
- **Q**, **Escape** alebo tlačidlo Zavrieť vrátia ovládanie hráča. Klikanie v otvorenom inventári predmety nevyhadzuje. Po zavretí vyberáš aj kolieskom alebo klávesmi **1–9**; posledné tri políčka vyberieš myšou alebo kolieskom.
- **H** v produkčnom sklade otvorí postup pestovania a balenia. Návod je dostupný aj tlačidlom v inventári, po slovensky aj anglicky.
- Zber výbavy: **E**. Vybrané semienko pri prázdnom kvetináči: **F**. Vybraná krhla pri vode: **E**, pri zasadenej rastline: **C**. Polej do 45 sekúnd; rast po poliatí trvá približne 18 sekúnd. S vybranými nožnicami zober zrelú úrodu cez **E**. Hrabľami cez **E** odstrániš suchú rastlinu.
- S úrodou a prázdnym vreckom pozri na baliaci stôl a stlač **E**; hotový balíček sa pridá rovno do inventára. Na ulici **G** ponúka vybraný produkt zákazníkovi; **X** ho spotrebuje. Ľavý klik počas hrania predmet vyhadzuje.
- Počas otvoreného inventára čas hry a rast rastlín pokračujú. Pôvodné misie a ich umiestnenie zostávajú zachované.

### Zjednodušený sklad

Sklad má dva vyvýšené kvetináče, jeden zdroj vody, baliaci stôl a jeden označený stôl s piatimi zásobami. Miešanie, ďalšie plodiny, prísady, nábytok na oddych a prepínače svetiel sú zo skladu odstránené. Semienka a vrecká sa doplnia po dvoch sekundách. Výbava zostáva na mieste namiesto padania za stoly.

Výber používa polohu predmetu na obrazovke, dosah od hráča a kontrolu prekážok. Funguje aj pri kamere za hráčom. Zbieranie má prednosť pred baliacim stolom a vodou, aby jedno E nespustilo viac úkonov. Po pozbieraní oboch plodov sa kvetináč uvoľní; dekoratívne listy ho už neblokujú.
