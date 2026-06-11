# Guide UI — Visual Mockups

Inline SVG mockups of the components catalogued in [`01-catalog.md`](./01-catalog.md). Each
mockup uses the real design-token colors:

| Token | Hex | Used for |
|---|---|---|
| `--guide-perfect-color` | `#2090c0` | "Perfect" performance, above-the-bar play |
| `--guide-good-color`    | `#4ec04e` | "Good" / target performance |
| `--guide-ok-color`      | `#ffc84a` | "Ok" / minor issue |
| `--guide-bad-color`     | `#ac1f39` | "Fail" / clear mistake |
| `--guide-very-bad-color`| `#661111` | Chart accent for severe loss |
| `--guide-mediocre-color`| `#dd5533` | Chart accent for partial mitigation / mediocre |
| `--guide-available-color`| `#696864`| Cooldown was ready / unused capacity |

Page background is approximated as `#1a1a1a`; section panels as `#222`–`#2a2a2a`.
All mockups are framework-neutral — they show layout, hierarchy, and color usage, not React/CSS-specific structure.

---

## 0. Cross-cutting tokens

### 0.1 PerformanceMark and PassFailCheckmark

The four canonical "performance dot" glyphs, plus the binary pass/fail variant.

<svg xmlns="http://www.w3.org/2000/svg" width="560" height="100" viewBox="0 0 560 100">
  <rect width="560" height="100" fill="#1a1a1a"/>
  <!-- Perfect -->
  <text x="60" y="22" fill="#bbb" text-anchor="middle" font-family="sans-serif" font-size="11">Perfect</text>
  <circle cx="60" cy="58" r="16" fill="none" stroke="#2090c0" stroke-width="2.5"/>
  <path d="M51,58 L57,64 L70,49" fill="none" stroke="#2090c0" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"/>
  <text x="60" y="92" fill="#666" text-anchor="middle" font-family="sans-serif" font-size="9">#2090c0</text>
  <!-- Good -->
  <text x="170" y="22" fill="#bbb" text-anchor="middle" font-family="sans-serif" font-size="11">Good</text>
  <path d="M158,58 L166,66 L184,46" fill="none" stroke="#4ec04e" stroke-width="3.5" stroke-linecap="round" stroke-linejoin="round"/>
  <text x="170" y="92" fill="#666" text-anchor="middle" font-family="sans-serif" font-size="9">#4ec04e</text>
  <!-- Ok -->
  <text x="280" y="22" fill="#bbb" text-anchor="middle" font-family="sans-serif" font-size="11">Ok</text>
  <text x="280" y="70" fill="#ffc84a" text-anchor="middle" font-family="sans-serif" font-size="34" font-weight="bold">*</text>
  <text x="280" y="92" fill="#666" text-anchor="middle" font-family="sans-serif" font-size="9">#ffc84a</text>
  <!-- Fail -->
  <text x="390" y="22" fill="#bbb" text-anchor="middle" font-family="sans-serif" font-size="11">Fail</text>
  <path d="M378,46 L402,70 M402,46 L378,70" stroke="#ac1f39" stroke-width="3.5" stroke-linecap="round"/>
  <text x="390" y="92" fill="#666" text-anchor="middle" font-family="sans-serif" font-size="9">#ac1f39</text>
  <!-- Pass/Fail -->
  <line x1="450" y1="20" x2="450" y2="80" stroke="#333" stroke-width="1"/>
  <text x="505" y="22" fill="#bbb" text-anchor="middle" font-family="sans-serif" font-size="11">PassFailCheckmark</text>
  <path d="M475,58 L482,66 L495,48" fill="none" stroke="#4ec04e" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"/>
  <path d="M515,46 L535,68 M535,46 L515,68" stroke="#ac1f39" stroke-width="3" stroke-linecap="round"/>
</svg>

### 0.2 BoxRowEntry — the universal performance record

One colored box per event. Color is driven by `value: QualitativePerformance`. Tooltip on hover.

<svg xmlns="http://www.w3.org/2000/svg" width="560" height="130" viewBox="0 0 560 130">
  <rect width="560" height="130" fill="#1a1a1a"/>
  <text x="20" y="22" fill="#bbb" font-family="sans-serif" font-size="11">A single BoxRowEntry — color = value, tooltip on hover</text>
  <rect x="20" y="38" width="32" height="32" fill="#2090c0"/>
  <text x="60" y="58" fill="#888" font-family="sans-serif" font-size="11">value: Perfect</text>
  <!-- selected variant -->
  <text x="20" y="92" fill="#bbb" font-family="sans-serif" font-size="11">Selected state (className="selected"):</text>
  <rect x="20" y="100" width="32" height="32" fill="#ffc84a" stroke="#fff" stroke-width="3"/>
  <text x="60" y="120" fill="#888" font-family="sans-serif" font-size="11">value: Ok &nbsp; className: selected</text>
  <!-- tooltip indicator -->
  <g transform="translate(280,38)">
    <rect width="32" height="32" fill="#4ec04e"/>
    <g transform="translate(40,-30)">
      <rect width="190" height="60" rx="3" fill="#0d0d0d" stroke="#444" stroke-width="1"/>
      <text x="10" y="20" fill="#ddd" font-family="sans-serif" font-size="11" font-weight="bold">Cast @ 02:14.3</text>
      <text x="10" y="38" fill="#bbb" font-family="sans-serif" font-size="11">3 of 3 checks passed</text>
      <text x="10" y="52" fill="#888" font-family="sans-serif" font-size="10">(tooltip ReactNode)</text>
    </g>
  </g>
</svg>

### 0.3 Theme containers — SectionContainer / RoundedPanel / PerformanceRoundedPanel

<svg xmlns="http://www.w3.org/2000/svg" width="720" height="200" viewBox="0 0 720 200">
  <rect width="720" height="200" fill="#1a1a1a"/>
  <!-- SectionContainer -->
  <rect x="10" y="20" width="220" height="160" rx="8" fill="rgba(30,30,30,0.8)" stroke="#333"/>
  <text x="120" y="44" fill="#ddd" text-anchor="middle" font-family="sans-serif" font-size="12" font-weight="bold">SectionContainer</text>
  <text x="120" y="74" fill="#777" text-anchor="middle" font-family="sans-serif" font-size="10">dark translucent, r=8</text>
  <text x="120" y="92" fill="#777" text-anchor="middle" font-family="sans-serif" font-size="10">default card</text>
  <text x="120" y="120" fill="#555" text-anchor="middle" font-family="sans-serif" font-size="10">10/12 padding</text>
  <!-- RoundedPanel -->
  <rect x="250" y="20" width="220" height="160" rx="8" fill="#222" stroke="#2a2a2a"/>
  <text x="360" y="44" fill="#ddd" text-anchor="middle" font-family="sans-serif" font-size="12" font-weight="bold">RoundedPanel</text>
  <text x="360" y="74" fill="#777" text-anchor="middle" font-family="sans-serif" font-size="10">slightly darker (#222)</text>
  <text x="360" y="92" fill="#777" text-anchor="middle" font-family="sans-serif" font-size="10">grid-based content</text>
  <text x="360" y="120" fill="#555" text-anchor="middle" font-family="sans-serif" font-size="10">inside 2-col layouts</text>
  <!-- PerformanceRoundedPanel -->
  <defs>
    <linearGradient id="perfShadow" x1="0%" y1="0%" x2="20%" y2="0%">
      <stop offset="0%" stop-color="#4ec04e" stop-opacity="0.6"/>
      <stop offset="100%" stop-color="#4ec04e" stop-opacity="0"/>
    </linearGradient>
  </defs>
  <rect x="490" y="20" width="220" height="160" rx="8" fill="#222" stroke="#2a2a2a"/>
  <rect x="490" y="20" width="220" height="160" rx="8" fill="url(#perfShadow)"/>
  <text x="600" y="44" fill="#ddd" text-anchor="middle" font-family="sans-serif" font-size="12" font-weight="bold">PerformanceRoundedPanel</text>
  <text x="600" y="74" fill="#777" text-anchor="middle" font-family="sans-serif" font-size="10">inset left shadow tinted</text>
  <text x="600" y="92" fill="#777" text-anchor="middle" font-family="sans-serif" font-size="10">by QualitativePerformance</text>
  <text x="600" y="120" fill="#555" text-anchor="middle" font-family="sans-serif" font-size="10">(here: Good)</text>
</svg>

---

## 1. Foundation: section structure

### 1.1 Section (expandable) and 1.2 SubSection

Expandable yellow-titled card containing one or more non-collapsible SubSections.

<svg xmlns="http://www.w3.org/2000/svg" width="720" height="380" viewBox="0 0 720 380">
  <rect width="720" height="380" fill="#1a1a1a"/>
  <!-- Section outer -->
  <rect x="10" y="10" width="700" height="360" rx="4" fill="#1f1f1f" stroke="#3a3a3a"/>
  <!-- Section title bar -->
  <rect x="10" y="10" width="700" height="40" rx="4" fill="#2a2a1a"/>
  <text x="30" y="36" fill="#ffd34a" font-family="sans-serif" font-size="15" font-weight="bold">Core Skills</text>
  <text x="685" y="36" fill="#ffd34a" text-anchor="end" font-family="sans-serif" font-size="16">▼</text>
  <!-- SubSection 1 -->
  <text x="30" y="80" fill="#ddd" font-family="sans-serif" font-size="13" font-weight="bold">Rotation</text>
  <rect x="30" y="92" width="660" height="80" fill="#252525" rx="4"/>
  <text x="45" y="116" fill="#888" font-family="sans-serif" font-size="11">SubSection body — explanation + data here</text>
  <text x="45" y="134" fill="#666" font-family="sans-serif" font-size="10">(typically an ExplanationRow inside)</text>
  <text x="45" y="152" fill="#666" font-family="sans-serif" font-size="10">paddingBottom: 20 by convention</text>
  <!-- SubSection 2 -->
  <text x="30" y="200" fill="#ddd" font-family="sans-serif" font-size="13" font-weight="bold">Cooldowns</text>
  <rect x="30" y="212" width="660" height="80" fill="#252525" rx="4"/>
  <text x="45" y="236" fill="#888" font-family="sans-serif" font-size="11">SubSection body</text>
  <!-- SubSection 3 -->
  <text x="30" y="320" fill="#ddd" font-family="sans-serif" font-size="13" font-weight="bold">Defensives</text>
  <rect x="30" y="332" width="660" height="30" fill="#252525" rx="4"/>
  <!-- annotation -->
  <text x="500" y="80" fill="#555" font-family="sans-serif" font-size="10" font-style="italic">Section: collapsible, yellow header</text>
  <text x="500" y="200" fill="#555" font-family="sans-serif" font-size="10" font-style="italic">3–5 SubSections per Section is typical</text>
</svg>

### 1.3 ExplanationRow — the canonical two-column pattern

The dominant content layout: prose on the left, data/visual on the right.
Default split is 30 / 70 (narrow explanation); `wideExplanation` flips it to 50 / 50.

<svg xmlns="http://www.w3.org/2000/svg" width="720" height="220" viewBox="0 0 720 220">
  <rect width="720" height="220" fill="#1a1a1a"/>
  <!-- 30/70 default -->
  <text x="10" y="20" fill="#bbb" font-family="sans-serif" font-size="11" font-weight="bold">Default: 30% explanation / 70% data</text>
  <rect x="10" y="30" width="207" height="70" fill="#222" rx="4"/>
  <text x="20" y="50" fill="#ddd" font-family="sans-serif" font-size="11">Explanation</text>
  <text x="20" y="68" fill="#888" font-family="sans-serif" font-size="10">Prose, links to spells,</text>
  <text x="20" y="82" fill="#888" font-family="sans-serif" font-size="10">why this matters.</text>
  <rect x="223" y="30" width="487" height="70" fill="#2a2a2a" rx="4"/>
  <text x="233" y="50" fill="#ddd" font-family="sans-serif" font-size="11">Data panel</text>
  <text x="233" y="68" fill="#888" font-family="sans-serif" font-size="10">RoundedPanel: stats, boxes, charts, cast breakdown…</text>
  <text x="120" y="118" fill="#666" text-anchor="middle" font-family="monospace" font-size="10">30%</text>
  <text x="465" y="118" fill="#666" text-anchor="middle" font-family="monospace" font-size="10">70%</text>
  <!-- 50/50 wide -->
  <text x="10" y="148" fill="#bbb" font-family="sans-serif" font-size="11" font-weight="bold">wideExplanation: 50 / 50</text>
  <rect x="10" y="158" width="345" height="50" fill="#222" rx="4"/>
  <text x="20" y="178" fill="#ddd" font-family="sans-serif" font-size="11">Explanation (wider — heavy prose, multiple paragraphs)</text>
  <rect x="365" y="158" width="345" height="50" fill="#2a2a2a" rx="4"/>
  <text x="375" y="178" fill="#ddd" font-family="sans-serif" font-size="11">Data panel</text>
</svg>

### 1.4 GuideSection (spell-focused)

Per-spell wrapper: spell title + explanation + data panel. Horizontal default, vertical option.

<svg xmlns="http://www.w3.org/2000/svg" width="720" height="180" viewBox="0 0 720 180">
  <rect width="720" height="180" fill="#1a1a1a"/>
  <!-- Spell title block -->
  <rect x="10" y="10" width="700" height="36" fill="#1c1c1c" rx="4"/>
  <rect x="20" y="20" width="20" height="20" fill="#7a5fff" rx="2"/>
  <text x="50" y="35" fill="#fff" font-family="sans-serif" font-size="13" font-weight="bold">Lava Burst</text>
  <text x="160" y="35" fill="#888" font-family="sans-serif" font-size="11">— title slot can include cast count, perf badge, etc.</text>
  <!-- Body two-column -->
  <rect x="10" y="56" width="280" height="110" fill="#222" rx="4"/>
  <text x="20" y="78" fill="#ddd" font-family="sans-serif" font-size="11">Explanation</text>
  <text x="20" y="96" fill="#888" font-family="sans-serif" font-size="10">When to cast, talent</text>
  <text x="20" y="110" fill="#888" font-family="sans-serif" font-size="10">interactions, priority.</text>
  <rect x="298" y="56" width="412" height="110" fill="#262626" rx="6" stroke="#333"/>
  <text x="310" y="78" fill="#ddd" font-family="sans-serif" font-size="11">Data RoundedPanel</text>
  <text x="310" y="98" fill="#888" font-family="sans-serif" font-size="10">Cast count, uptime, breakdown — whatever the spec puts in.</text>
  <text x="310" y="116" fill="#666" font-family="sans-serif" font-size="10">Often hosts a PerformanceBoxRow or StatsGrid.</text>
</svg>

---

## 2. Data wrappers & stat presentation

### 2.1 GuideDataWrapper

The standard "titled stat panel" — title, optional subtitle/icon, a `StatsRow` body, optional `helperText` footer.

<svg xmlns="http://www.w3.org/2000/svg" width="720" height="260" viewBox="0 0 720 260">
  <rect width="720" height="260" fill="#1a1a1a"/>
  <!-- Outer wrapper -->
  <rect x="10" y="10" width="700" height="240" rx="8" fill="rgba(30,30,30,0.85)" stroke="#333"/>
  <!-- Header row -->
  <rect x="20" y="20" width="40" height="40" fill="#3a3a3a" rx="4"/>
  <text x="40" y="46" fill="#ccc" text-anchor="middle" font-family="sans-serif" font-size="20">★</text>
  <text x="74" y="40" fill="#fff" font-family="sans-serif" font-size="15" font-weight="bold">Damage Cooldowns</text>
  <text x="74" y="58" fill="#888" font-family="sans-serif" font-size="11">Burst windows during the fight</text>
  <!-- Divider -->
  <line x1="20" y1="78" x2="690" y2="78" stroke="#2a2a2a"/>
  <!-- StatsRow (three StatCards) -->
  <g transform="translate(20,92)">
    <!-- card 1 -->
    <rect x="0" y="0" width="210" height="100" rx="6" fill="#1c1c1c" stroke="#4ec04e" stroke-width="2"/>
    <text x="105" y="26" fill="#aaa" text-anchor="middle" font-family="sans-serif" font-size="11">Casts</text>
    <text x="105" y="62" fill="#4ec04e" text-anchor="middle" font-family="sans-serif" font-size="30" font-weight="bold">12</text>
    <line x1="50" y1="72" x2="160" y2="72" stroke="#333"/>
    <text x="105" y="90" fill="#888" text-anchor="middle" font-family="sans-serif" font-size="10">of 13 possible</text>
    <!-- card 2 -->
    <rect x="225" y="0" width="210" height="100" rx="6" fill="#1c1c1c" stroke="#ffc84a" stroke-width="2"/>
    <text x="330" y="26" fill="#aaa" text-anchor="middle" font-family="sans-serif" font-size="11">Avg DPS</text>
    <text x="330" y="62" fill="#ffc84a" text-anchor="middle" font-family="sans-serif" font-size="30" font-weight="bold">1.4M</text>
    <line x1="275" y1="72" x2="385" y2="72" stroke="#333"/>
    <text x="330" y="90" fill="#888" text-anchor="middle" font-family="sans-serif" font-size="10">target: 1.6M</text>
    <!-- card 3 -->
    <rect x="450" y="0" width="210" height="100" rx="6" fill="#1c1c1c" stroke="#ac1f39" stroke-width="2"/>
    <text x="555" y="26" fill="#aaa" text-anchor="middle" font-family="sans-serif" font-size="11">Wasted</text>
    <text x="555" y="62" fill="#ac1f39" text-anchor="middle" font-family="sans-serif" font-size="30" font-weight="bold">3</text>
    <line x1="500" y1="72" x2="610" y2="72" stroke="#333"/>
    <text x="555" y="90" fill="#888" text-anchor="middle" font-family="sans-serif" font-size="10">CDs while capped</text>
  </g>
  <!-- helperText -->
  <line x1="20" y1="208" x2="690" y2="208" stroke="#2a2a2a"/>
  <text x="20" y="232" fill="#888" font-family="sans-serif" font-size="11">Helper text — short interpretive note shown under the stats grid.</text>
</svg>

### 2.2 StatCard variants — color = performance

Each card pulls its accent color from a `QualitativePerformance`.

<svg xmlns="http://www.w3.org/2000/svg" width="720" height="150" viewBox="0 0 720 150">
  <rect width="720" height="150" fill="#1a1a1a"/>
  <g font-family="sans-serif">
    <!-- Perfect -->
    <rect x="10" y="20" width="160" height="110" rx="6" fill="#1c1c1c" stroke="#2090c0" stroke-width="2"/>
    <text x="90" y="42" fill="#aaa" text-anchor="middle" font-size="11">Uptime</text>
    <text x="90" y="78" fill="#2090c0" text-anchor="middle" font-size="28" font-weight="bold">99%</text>
    <line x1="40" y1="92" x2="140" y2="92" stroke="#333"/>
    <text x="90" y="110" fill="#888" text-anchor="middle" font-size="10">target: 95%</text>
    <!-- Good -->
    <rect x="190" y="20" width="160" height="110" rx="6" fill="#1c1c1c" stroke="#4ec04e" stroke-width="2"/>
    <text x="270" y="42" fill="#aaa" text-anchor="middle" font-size="11">Uptime</text>
    <text x="270" y="78" fill="#4ec04e" text-anchor="middle" font-size="28" font-weight="bold">96%</text>
    <line x1="220" y1="92" x2="320" y2="92" stroke="#333"/>
    <text x="270" y="110" fill="#888" text-anchor="middle" font-size="10">target: 95%</text>
    <!-- Ok -->
    <rect x="370" y="20" width="160" height="110" rx="6" fill="#1c1c1c" stroke="#ffc84a" stroke-width="2"/>
    <text x="450" y="42" fill="#aaa" text-anchor="middle" font-size="11">Uptime</text>
    <text x="450" y="78" fill="#ffc84a" text-anchor="middle" font-size="28" font-weight="bold">88%</text>
    <line x1="400" y1="92" x2="500" y2="92" stroke="#333"/>
    <text x="450" y="110" fill="#888" text-anchor="middle" font-size="10">target: 95%</text>
    <!-- Fail -->
    <rect x="550" y="20" width="160" height="110" rx="6" fill="#1c1c1c" stroke="#ac1f39" stroke-width="2"/>
    <text x="630" y="42" fill="#aaa" text-anchor="middle" font-size="11">Uptime</text>
    <text x="630" y="78" fill="#ac1f39" text-anchor="middle" font-size="28" font-weight="bold">74%</text>
    <line x1="580" y1="92" x2="680" y2="92" stroke="#333"/>
    <text x="630" y="110" fill="#888" text-anchor="middle" font-size="10">target: 95%</text>
  </g>
</svg>

### 2.3 StatsGrid — multiple StatCards arranged as a grid

When you want to show 4+ stats at once. Cards wrap onto multiple rows.

<svg xmlns="http://www.w3.org/2000/svg" width="720" height="280" viewBox="0 0 720 280">
  <rect width="720" height="280" fill="#1a1a1a"/>
  <rect x="10" y="10" width="700" height="260" rx="8" fill="rgba(30,30,30,0.85)" stroke="#333"/>
  <text x="20" y="34" fill="#fff" font-family="sans-serif" font-size="13" font-weight="bold">Resource Management</text>
  <g font-family="sans-serif" transform="translate(20,46)">
    <!-- row 1 -->
    <g>
      <rect x="0"   y="0" width="160" height="90" rx="6" fill="#1c1c1c" stroke="#4ec04e" stroke-width="2"/>
      <text x="80" y="22" fill="#aaa" text-anchor="middle" font-size="11">Avg Maelstrom</text>
      <text x="80" y="58" fill="#4ec04e" text-anchor="middle" font-size="24" font-weight="bold">62</text>
      <text x="80" y="78" fill="#777" text-anchor="middle" font-size="10">of 100</text>
      <rect x="170" y="0" width="160" height="90" rx="6" fill="#1c1c1c" stroke="#ffc84a" stroke-width="2"/>
      <text x="250" y="22" fill="#aaa" text-anchor="middle" font-size="11">Capped time</text>
      <text x="250" y="58" fill="#ffc84a" text-anchor="middle" font-size="24" font-weight="bold">12%</text>
      <text x="250" y="78" fill="#777" text-anchor="middle" font-size="10">target &lt; 5%</text>
      <rect x="340" y="0" width="160" height="90" rx="6" fill="#1c1c1c" stroke="#4ec04e" stroke-width="2"/>
      <text x="420" y="22" fill="#aaa" text-anchor="middle" font-size="11">Spent</text>
      <text x="420" y="58" fill="#4ec04e" text-anchor="middle" font-size="24" font-weight="bold">4,210</text>
      <text x="420" y="78" fill="#777" text-anchor="middle" font-size="10">/ 4,400 generated</text>
      <rect x="510" y="0" width="160" height="90" rx="6" fill="#1c1c1c" stroke="#ac1f39" stroke-width="2"/>
      <text x="590" y="22" fill="#aaa" text-anchor="middle" font-size="11">Wasted</text>
      <text x="590" y="58" fill="#ac1f39" text-anchor="middle" font-size="24" font-weight="bold">190</text>
      <text x="590" y="78" fill="#777" text-anchor="middle" font-size="10">capped overflow</text>
    </g>
    <!-- row 2 -->
    <g transform="translate(0,110)">
      <rect x="0"   y="0" width="160" height="90" rx="6" fill="#1c1c1c" stroke="#2090c0" stroke-width="2"/>
      <text x="80" y="22" fill="#aaa" text-anchor="middle" font-size="11">Procs used</text>
      <text x="80" y="58" fill="#2090c0" text-anchor="middle" font-size="24" font-weight="bold">100%</text>
      <text x="80" y="78" fill="#777" text-anchor="middle" font-size="10">14 / 14</text>
      <rect x="170" y="0" width="160" height="90" rx="6" fill="#1c1c1c" stroke="#4ec04e" stroke-width="2"/>
      <text x="250" y="22" fill="#aaa" text-anchor="middle" font-size="11">CDR triggered</text>
      <text x="250" y="58" fill="#4ec04e" text-anchor="middle" font-size="24" font-weight="bold">88%</text>
      <text x="250" y="78" fill="#777" text-anchor="middle" font-size="10">target &gt; 80%</text>
    </g>
  </g>
</svg>

### 2.4 PerfBadgeGrid — counts per QualitativePerformance bucket

A compact "tally" view: how many events fell into each grade. Each badge has its own color.

<svg xmlns="http://www.w3.org/2000/svg" width="720" height="120" viewBox="0 0 720 120">
  <rect width="720" height="120" fill="#1a1a1a"/>
  <rect x="10" y="10" width="700" height="100" rx="8" fill="rgba(30,30,30,0.85)" stroke="#333"/>
  <text x="20" y="36" fill="#ccc" font-family="sans-serif" font-size="12" font-weight="bold">Cast quality</text>
  <g font-family="sans-serif" transform="translate(160,28)">
    <rect x="0"   y="0" width="120" height="60" rx="4" fill="#1c1c1c" stroke="#2090c0" stroke-width="2"/>
    <text x="60" y="28" fill="#2090c0" text-anchor="middle" font-size="22" font-weight="bold">9</text>
    <text x="60" y="48" fill="#aaa" text-anchor="middle" font-size="11">Perfect</text>
    <rect x="135" y="0" width="120" height="60" rx="4" fill="#1c1c1c" stroke="#4ec04e" stroke-width="2"/>
    <text x="195" y="28" fill="#4ec04e" text-anchor="middle" font-size="22" font-weight="bold">14</text>
    <text x="195" y="48" fill="#aaa" text-anchor="middle" font-size="11">Good</text>
    <rect x="270" y="0" width="120" height="60" rx="4" fill="#1c1c1c" stroke="#ffc84a" stroke-width="2"/>
    <text x="330" y="28" fill="#ffc84a" text-anchor="middle" font-size="22" font-weight="bold">3</text>
    <text x="330" y="48" fill="#aaa" text-anchor="middle" font-size="11">Ok</text>
    <rect x="405" y="0" width="120" height="60" rx="4" fill="#1c1c1c" stroke="#ac1f39" stroke-width="2"/>
    <text x="465" y="28" fill="#ac1f39" text-anchor="middle" font-size="22" font-weight="bold">2</text>
    <text x="465" y="48" fill="#aaa" text-anchor="middle" font-size="11">Fail</text>
  </g>
</svg>

---

## 3. Cast analysis

### 3.1 CastSummary — bar of "graded cast bins"

A single horizontal bar where each colored slice is one bucket (Perfect/Good/Ok/Fail) sized
proportionally to its share of casts. Hover shows the bucket count.

<svg xmlns="http://www.w3.org/2000/svg" width="720" height="120" viewBox="0 0 720 120">
  <rect width="720" height="120" fill="#1a1a1a"/>
  <text x="20" y="30" fill="#ddd" font-family="sans-serif" font-size="12" font-weight="bold">Cast Summary — 28 casts</text>
  <!-- bar -->
  <g transform="translate(20,46)">
    <rect x="0"   y="0" width="220" height="34" fill="#2090c0"/>
    <rect x="220" y="0" width="320" height="34" fill="#4ec04e"/>
    <rect x="540" y="0" width="80"  height="34" fill="#ffc84a"/>
    <rect x="620" y="0" width="60"  height="34" fill="#ac1f39"/>
    <text x="110" y="22" fill="#fff" text-anchor="middle" font-family="sans-serif" font-size="11" font-weight="bold">9 Perfect</text>
    <text x="380" y="22" fill="#fff" text-anchor="middle" font-family="sans-serif" font-size="11" font-weight="bold">14 Good</text>
    <text x="580" y="22" fill="#000" text-anchor="middle" font-family="sans-serif" font-size="11" font-weight="bold">3 Ok</text>
    <text x="650" y="22" fill="#fff" text-anchor="middle" font-family="sans-serif" font-size="11" font-weight="bold">2</text>
  </g>
  <!-- legend ticks -->
  <g transform="translate(20,90)" font-family="sans-serif" font-size="10" fill="#777">
    <text x="0">0</text>
    <text x="660">100%</text>
  </g>
</svg>

### 3.2 CastOverview — bucketed cast list with per-bucket bars

Shows multiple sub-bars (one per ability or category) with the same Perfect/Good/Ok/Fail breakdown.

<svg xmlns="http://www.w3.org/2000/svg" width="720" height="240" viewBox="0 0 720 240">
  <rect width="720" height="240" fill="#1a1a1a"/>
  <rect x="10" y="10" width="700" height="220" rx="8" fill="rgba(30,30,30,0.85)" stroke="#333"/>
  <text x="20" y="34" fill="#fff" font-family="sans-serif" font-size="13" font-weight="bold">Cast Overview</text>
  <g font-family="sans-serif" transform="translate(20,50)">
    <!-- row 1 -->
    <text x="0" y="20" fill="#ddd" font-size="11">Lava Burst</text>
    <text x="130" y="20" fill="#888" font-size="10">28 casts</text>
    <g transform="translate(200,8)">
      <rect x="0"   y="0" width="220" height="18" fill="#2090c0"/>
      <rect x="220" y="0" width="180" height="18" fill="#4ec04e"/>
      <rect x="400" y="0" width="60"  height="18" fill="#ffc84a"/>
      <rect x="460" y="0" width="40"  height="18" fill="#ac1f39"/>
    </g>
    <!-- row 2 -->
    <g transform="translate(0,40)">
      <text x="0" y="20" fill="#ddd" font-size="11">Lightning Bolt</text>
      <text x="130" y="20" fill="#888" font-size="10">62 casts</text>
      <g transform="translate(200,8)">
        <rect x="0"   y="0" width="120" height="18" fill="#2090c0"/>
        <rect x="120" y="0" width="320" height="18" fill="#4ec04e"/>
        <rect x="440" y="0" width="40"  height="18" fill="#ffc84a"/>
        <rect x="480" y="0" width="20"  height="18" fill="#ac1f39"/>
      </g>
    </g>
    <!-- row 3 -->
    <g transform="translate(0,80)">
      <text x="0" y="20" fill="#ddd" font-size="11">Earth Shock</text>
      <text x="130" y="20" fill="#888" font-size="10">21 casts</text>
      <g transform="translate(200,8)">
        <rect x="0"   y="0" width="80"  height="18" fill="#2090c0"/>
        <rect x="80"  y="0" width="280" height="18" fill="#4ec04e"/>
        <rect x="360" y="0" width="100" height="18" fill="#ffc84a"/>
        <rect x="460" y="0" width="40"  height="18" fill="#ac1f39"/>
      </g>
    </g>
    <!-- row 4 -->
    <g transform="translate(0,120)">
      <text x="0" y="20" fill="#ddd" font-size="11">Flame Shock</text>
      <text x="130" y="20" fill="#888" font-size="10">9 casts</text>
      <g transform="translate(200,8)">
        <rect x="0"   y="0" width="380" height="18" fill="#2090c0"/>
        <rect x="380" y="0" width="60"  height="18" fill="#4ec04e"/>
        <rect x="440" y="0" width="60"  height="18" fill="#ac1f39"/>
      </g>
    </g>
  </g>
</svg>

### 3.3 CastDetail — per-cast tooltip / drill-down

Tabular form: each row is one cast, showing time, performance, key checks.

<svg xmlns="http://www.w3.org/2000/svg" width="720" height="240" viewBox="0 0 720 240">
  <rect width="720" height="240" fill="#1a1a1a"/>
  <rect x="10" y="10" width="700" height="220" rx="8" fill="rgba(30,30,30,0.85)" stroke="#333"/>
  <text x="20" y="34" fill="#fff" font-family="sans-serif" font-size="13" font-weight="bold">Lava Burst — per-cast detail</text>
  <g font-family="sans-serif" font-size="11" transform="translate(20,50)">
    <!-- header -->
    <text x="0"   y="14" fill="#888">Time</text>
    <text x="90"  y="14" fill="#888">Perf.</text>
    <text x="160" y="14" fill="#888">Maelstrom</text>
    <text x="280" y="14" fill="#888">Procs</text>
    <text x="380" y="14" fill="#888">Buffs active</text>
    <line x1="0" y1="22" x2="660" y2="22" stroke="#333"/>
    <!-- row 1 -->
    <text x="0"   y="44" fill="#ddd">00:08.2</text>
    <g transform="translate(90,28)">
      <path d="M0,16 L8,24 L22,8" stroke="#4ec04e" stroke-width="2.5" fill="none" stroke-linecap="round"/>
    </g>
    <text x="160" y="44" fill="#ddd">62</text>
    <text x="280" y="44" fill="#ddd">Stormkeeper</text>
    <text x="380" y="44" fill="#ddd">Ascendance, Master of the Elements</text>
    <!-- row 2 -->
    <text x="0"   y="74" fill="#ddd">00:18.1</text>
    <g transform="translate(90,58)">
      <circle cx="11" cy="16" r="11" fill="none" stroke="#2090c0" stroke-width="2"/>
      <path d="M5,16 L9,21 L18,12" stroke="#2090c0" stroke-width="2" fill="none" stroke-linecap="round"/>
    </g>
    <text x="160" y="74" fill="#ddd">98</text>
    <text x="280" y="74" fill="#ddd">—</text>
    <text x="380" y="74" fill="#ddd">Ascendance</text>
    <!-- row 3 -->
    <text x="0"   y="104" fill="#ddd">00:27.6</text>
    <g transform="translate(90,90)">
      <text x="11" y="20" fill="#ffc84a" text-anchor="middle" font-size="20" font-weight="bold">*</text>
    </g>
    <text x="160" y="104" fill="#ddd">40</text>
    <text x="280" y="104" fill="#ddd">—</text>
    <text x="380" y="104" fill="#ddd">—</text>
    <!-- row 4 -->
    <text x="0"   y="134" fill="#ddd">00:35.4</text>
    <g transform="translate(90,118)">
      <path d="M2,8 L20,24 M20,8 L2,24" stroke="#ac1f39" stroke-width="2.5" stroke-linecap="round"/>
    </g>
    <text x="160" y="134" fill="#ddd">22</text>
    <text x="280" y="134" fill="#ddd">—</text>
    <text x="380" y="134" fill="#ddd">—</text>
    <text x="0"   y="166" fill="#666" font-style="italic">…</text>
  </g>
</svg>

### 3.4 CastSequence — ordered colored chips

A compact strip showing the order of casts (left-to-right), color-coded by performance.
Useful for visualizing rotation order during a burst window.

<svg xmlns="http://www.w3.org/2000/svg" width="720" height="120" viewBox="0 0 720 120">
  <rect width="720" height="120" fill="#1a1a1a"/>
  <text x="20" y="30" fill="#ddd" font-family="sans-serif" font-size="12" font-weight="bold">Cast Sequence — Stormkeeper window</text>
  <g transform="translate(20,46)" font-family="sans-serif" font-size="10">
    <!-- 10 chips -->
    <g>
      <rect x="0"   y="0" width="60" height="40" rx="4" fill="#4ec04e"/>
      <text x="30" y="24" fill="#fff" text-anchor="middle" font-weight="bold">SK</text>
      <rect x="66"  y="0" width="60" height="40" rx="4" fill="#2090c0"/>
      <text x="96" y="24" fill="#fff" text-anchor="middle" font-weight="bold">LvB</text>
      <rect x="132" y="0" width="60" height="40" rx="4" fill="#2090c0"/>
      <text x="162" y="24" fill="#fff" text-anchor="middle" font-weight="bold">LB</text>
      <rect x="198" y="0" width="60" height="40" rx="4" fill="#4ec04e"/>
      <text x="228" y="24" fill="#fff" text-anchor="middle" font-weight="bold">ES</text>
      <rect x="264" y="0" width="60" height="40" rx="4" fill="#4ec04e"/>
      <text x="294" y="24" fill="#fff" text-anchor="middle" font-weight="bold">LB</text>
      <rect x="330" y="0" width="60" height="40" rx="4" fill="#ffc84a"/>
      <text x="360" y="24" fill="#000" text-anchor="middle" font-weight="bold">FS</text>
      <rect x="396" y="0" width="60" height="40" rx="4" fill="#2090c0"/>
      <text x="426" y="24" fill="#fff" text-anchor="middle" font-weight="bold">LvB</text>
      <rect x="462" y="0" width="60" height="40" rx="4" fill="#ac1f39"/>
      <text x="492" y="24" fill="#fff" text-anchor="middle" font-weight="bold">LB</text>
      <rect x="528" y="0" width="60" height="40" rx="4" fill="#4ec04e"/>
      <text x="558" y="24" fill="#fff" text-anchor="middle" font-weight="bold">LvB</text>
      <rect x="594" y="0" width="60" height="40" rx="4" fill="#4ec04e"/>
      <text x="624" y="24" fill="#fff" text-anchor="middle" font-weight="bold">ES</text>
    </g>
    <line x1="0" y1="52" x2="660" y2="52" stroke="#333"/>
    <text x="0"   y="68" fill="#777">0.0s</text>
    <text x="630" y="68" fill="#777">12.0s</text>
  </g>
</svg>

---

## 4. Visualizations

### 4.1 PerformanceBoxRow — the most distinctive primitive

A row of small colored squares, one per `BoxRowEntry`. Click selects an entry. Color = grade.

<svg xmlns="http://www.w3.org/2000/svg" width="720" height="160" viewBox="0 0 720 160">
  <rect width="720" height="160" fill="#1a1a1a"/>
  <text x="20" y="30" fill="#ddd" font-family="sans-serif" font-size="12" font-weight="bold">Cast Breakdown</text>
  <text x="120" y="30" fill="#888" font-family="sans-serif" font-size="10">— each box is one cast, click to see details below</text>
  <!-- row of boxes -->
  <g transform="translate(20,50)">
<!-- 40 boxes, mixed -->
    <g font-family="sans-serif">
      <rect x="0"   y="0" width="16" height="16" fill="#4ec04e"/>
      <rect x="18"  y="0" width="16" height="16" fill="#4ec04e"/>
      <rect x="36"  y="0" width="16" height="16" fill="#2090c0"/>
      <rect x="54"  y="0" width="16" height="16" fill="#4ec04e"/>
      <rect x="72"  y="0" width="16" height="16" fill="#ffc84a"/>
      <rect x="90"  y="0" width="16" height="16" fill="#4ec04e"/>
      <rect x="108" y="0" width="16" height="16" fill="#2090c0"/>
      <rect x="126" y="0" width="16" height="16" fill="#ac1f39"/>
      <rect x="144" y="0" width="16" height="16" fill="#4ec04e"/>
      <rect x="162" y="0" width="16" height="16" fill="#4ec04e"/>
      <rect x="180" y="0" width="16" height="16" fill="#ac1f39" stroke="#fff" stroke-width="2"/>
      <rect x="198" y="0" width="16" height="16" fill="#4ec04e"/>
      <rect x="216" y="0" width="16" height="16" fill="#2090c0"/>
      <rect x="234" y="0" width="16" height="16" fill="#4ec04e"/>
      <rect x="252" y="0" width="16" height="16" fill="#ffc84a"/>
      <rect x="270" y="0" width="16" height="16" fill="#4ec04e"/>
      <rect x="288" y="0" width="16" height="16" fill="#4ec04e"/>
      <rect x="306" y="0" width="16" height="16" fill="#2090c0"/>
      <rect x="324" y="0" width="16" height="16" fill="#4ec04e"/>
      <rect x="342" y="0" width="16" height="16" fill="#ac1f39"/>
      <rect x="360" y="0" width="16" height="16" fill="#4ec04e"/>
      <rect x="378" y="0" width="16" height="16" fill="#ffc84a"/>
      <rect x="396" y="0" width="16" height="16" fill="#2090c0"/>
      <rect x="414" y="0" width="16" height="16" fill="#4ec04e"/>
      <rect x="432" y="0" width="16" height="16" fill="#4ec04e"/>
      <rect x="450" y="0" width="16" height="16" fill="#2090c0"/>
      <rect x="468" y="0" width="16" height="16" fill="#4ec04e"/>
      <rect x="486" y="0" width="16" height="16" fill="#4ec04e"/>
      <rect x="504" y="0" width="16" height="16" fill="#ffc84a"/>
      <rect x="522" y="0" width="16" height="16" fill="#2090c0"/>
      <rect x="540" y="0" width="16" height="16" fill="#4ec04e"/>
      <rect x="558" y="0" width="16" height="16" fill="#ac1f39"/>
      <rect x="576" y="0" width="16" height="16" fill="#4ec04e"/>
      <rect x="594" y="0" width="16" height="16" fill="#4ec04e"/>
      <rect x="612" y="0" width="16" height="16" fill="#2090c0"/>
      <rect x="630" y="0" width="16" height="16" fill="#4ec04e"/>
    </g>
  </g>
  <!-- selected box callout -->
  <path d="M210,68 L210,98" stroke="#fff" stroke-width="1" stroke-dasharray="2,2"/>
  <text x="180" y="116" fill="#fff" font-family="sans-serif" font-size="10">selected</text>
  <text x="180" y="130" fill="#888" font-family="sans-serif" font-size="10">className: "selected"</text>
  <!-- key -->
  <g transform="translate(20,128)" font-family="sans-serif" font-size="10" fill="#aaa">
    <rect x="0" y="-9" width="10" height="10" fill="#2090c0"/><text x="14" y="0">Perfect</text>
    <rect x="64" y="-9" width="10" height="10" fill="#4ec04e"/><text x="78" y="0">Good</text>
    <rect x="124" y="-9" width="10" height="10" fill="#ffc84a"/><text x="138" y="0">Ok / saved</text>
    <rect x="208" y="-9" width="10" height="10" fill="#ac1f39"/><text x="222" y="0">Fail / missed</text>
  </g>
</svg>

### 4.2 StackedBar — proportional segments

Generic stacked-segment horizontal bar. Each segment carries its own color (`StackedBarSegment`).

<svg xmlns="http://www.w3.org/2000/svg" width="720" height="120" viewBox="0 0 720 120">
  <rect width="720" height="120" fill="#1a1a1a"/>
  <text x="20" y="28" fill="#ddd" font-family="sans-serif" font-size="12" font-weight="bold">Maelstrom spending breakdown</text>
  <g transform="translate(20,42)">
    <rect x="0"   y="0" width="180" height="30" fill="#4ec04e"/>
    <rect x="180" y="0" width="240" height="30" fill="#2090c0"/>
    <rect x="420" y="0" width="120" height="30" fill="#ffc84a"/>
    <rect x="540" y="0" width="60"  height="30" fill="#dd5533"/>
    <rect x="600" y="0" width="60"  height="30" fill="#696864"/>
    <text x="90"  y="20" fill="#000" text-anchor="middle" font-family="sans-serif" font-size="11" font-weight="bold">Lightning Bolt</text>
    <text x="300" y="20" fill="#fff" text-anchor="middle" font-family="sans-serif" font-size="11" font-weight="bold">Earth Shock</text>
    <text x="480" y="20" fill="#000" text-anchor="middle" font-family="sans-serif" font-size="11" font-weight="bold">Elemental Blast</text>
    <text x="570" y="20" fill="#fff" text-anchor="middle" font-family="sans-serif" font-size="11" font-weight="bold">EQ</text>
    <text x="630" y="20" fill="#fff" text-anchor="middle" font-family="sans-serif" font-size="11" font-weight="bold">Waste</text>
  </g>
  <text x="20" y="100" fill="#777" font-family="sans-serif" font-size="10">Each segment colored independently (any QualitativePerformance or chart color)</text>
</svg>

### 4.3 GradiatedPerformanceBar — a single bar that "fills up" through grades

Background = bar lane; filled portion is a horizontal gradient through the performance colors.

<svg xmlns="http://www.w3.org/2000/svg" width="720" height="180" viewBox="0 0 720 180">
  <rect width="720" height="180" fill="#1a1a1a"/>
  <defs>
    <linearGradient id="grad" x1="0%" y1="0%" x2="100%" y2="0%">
      <stop offset="0%" stop-color="#ac1f39"/>
      <stop offset="40%" stop-color="#ffc84a"/>
      <stop offset="75%" stop-color="#4ec04e"/>
      <stop offset="100%" stop-color="#2090c0"/>
    </linearGradient>
  </defs>
  <!-- ex 1 -->
  <text x="20" y="32" fill="#ddd" font-family="sans-serif" font-size="12">Bloodlust uptime</text>
  <rect x="20" y="42" width="660" height="22" rx="3" fill="#262626"/>
  <rect x="20" y="42" width="600" height="22" rx="3" fill="url(#grad)"/>
  <text x="640" y="59" fill="#fff" font-family="sans-serif" font-size="11" font-weight="bold">91%</text>
  <!-- ex 2 -->
  <text x="20" y="92" fill="#ddd" font-family="sans-serif" font-size="12">Earthen Wall uptime</text>
  <rect x="20" y="102" width="660" height="22" rx="3" fill="#262626"/>
  <rect x="20" y="102" width="350" height="22" rx="3" fill="url(#grad)"/>
  <text x="640" y="119" fill="#aaa" font-family="sans-serif" font-size="11" font-weight="bold">53%</text>
  <!-- ex 3 -->
  <text x="20" y="152" fill="#ddd" font-family="sans-serif" font-size="12">Spirit Beast uptime</text>
  <rect x="20" y="162" width="660" height="14" rx="3" fill="#262626"/>
  <rect x="20" y="162" width="180" height="14" rx="3" fill="url(#grad)"/>
</svg>

### 4.4 PassFailBar — binary "pass vs fail" bar

A simpler bar: just one solid color, green or red.

<svg xmlns="http://www.w3.org/2000/svg" width="720" height="110" viewBox="0 0 720 110">
  <rect width="720" height="110" fill="#1a1a1a"/>
  <text x="20" y="28" fill="#ddd" font-family="sans-serif" font-size="12">Interrupts caught</text>
  <rect x="20" y="38" width="660" height="20" rx="3" fill="#262626"/>
  <rect x="20" y="38" width="528" height="20" rx="3" fill="#4ec04e"/>
  <text x="556" y="54" fill="#aaa" font-family="sans-serif" font-size="11">8 / 10</text>
  <text x="20" y="82" fill="#ddd" font-family="sans-serif" font-size="12">Mechanic dodged</text>
  <rect x="20" y="92" width="660" height="14" rx="3" fill="#ac1f39"/>
  <text x="640" y="103" fill="#fff" font-family="sans-serif" font-size="10" font-weight="bold">FAIL</text>
</svg>

### 4.5 BuffUptimeBar — fight-long timeline of buff windows

SVG strip from `fightStart` → `fightEnd`. Filled bands are when the buff was up; gaps = downtime.
Used for class buffs, defensives, externals.

<svg xmlns="http://www.w3.org/2000/svg" width="720" height="180" viewBox="0 0 720 180">
  <rect width="720" height="180" fill="#1a1a1a"/>
  <!-- title -->
  <text x="20" y="28" fill="#ddd" font-family="sans-serif" font-size="12" font-weight="bold">Buff timelines (fightStart → fightEnd)</text>
  <!-- track 1 -->
  <text x="20" y="58" fill="#ddd" font-family="sans-serif" font-size="11">Ascendance</text>
  <rect x="130" y="48" width="560" height="14" fill="#262626" rx="2"/>
  <rect x="140" y="48" width="60"  height="14" fill="#4ec04e"/>
  <rect x="260" y="48" width="60"  height="14" fill="#4ec04e"/>
  <rect x="400" y="48" width="60"  height="14" fill="#4ec04e"/>
  <rect x="540" y="48" width="60"  height="14" fill="#4ec04e"/>
  <!-- track 2 -->
  <text x="20" y="88" fill="#ddd" font-family="sans-serif" font-size="11">Stormkeeper</text>
  <rect x="130" y="78" width="560" height="14" fill="#262626" rx="2"/>
  <rect x="140" y="78" width="20" height="14" fill="#2090c0"/>
  <rect x="220" y="78" width="20" height="14" fill="#2090c0"/>
  <rect x="360" y="78" width="20" height="14" fill="#2090c0"/>
  <rect x="460" y="78" width="20" height="14" fill="#2090c0"/>
  <rect x="560" y="78" width="20" height="14" fill="#2090c0"/>
  <rect x="640" y="78" width="20" height="14" fill="#2090c0"/>
  <!-- track 3 -->
  <text x="20" y="118" fill="#ddd" font-family="sans-serif" font-size="11">Bloodlust</text>
  <rect x="130" y="108" width="560" height="14" fill="#262626" rx="2"/>
  <rect x="140" y="108" width="170" height="14" fill="#dd5533"/>
  <!-- track 4 (defensive) -->
  <text x="20" y="148" fill="#ddd" font-family="sans-serif" font-size="11">Astral Shift</text>
  <rect x="130" y="138" width="560" height="14" fill="#262626" rx="2"/>
  <rect x="180" y="138" width="20" height="14" fill="#4ec04e"/>
  <rect x="330" y="138" width="20" height="14" fill="#ffc84a"/>
  <rect x="510" y="138" width="20" height="14" fill="#ac1f39"/>
  <!-- timeline ticks -->
  <line x1="130" y1="160" x2="690" y2="160" stroke="#444"/>
  <line x1="130" y1="158" x2="130" y2="166" stroke="#777"/>
  <line x1="270" y1="158" x2="270" y2="166" stroke="#777"/>
  <line x1="410" y1="158" x2="410" y2="166" stroke="#777"/>
  <line x1="550" y1="158" x2="550" y2="166" stroke="#777"/>
  <line x1="690" y1="158" x2="690" y2="166" stroke="#777"/>
  <text x="130" y="178" fill="#777" font-family="sans-serif" font-size="9" text-anchor="middle">00:00</text>
  <text x="270" y="178" fill="#777" font-family="sans-serif" font-size="9" text-anchor="middle">01:00</text>
  <text x="410" y="178" fill="#777" font-family="sans-serif" font-size="9" text-anchor="middle">02:00</text>
  <text x="550" y="178" fill="#777" font-family="sans-serif" font-size="9" text-anchor="middle">03:00</text>
  <text x="690" y="178" fill="#777" font-family="sans-serif" font-size="9" text-anchor="middle">03:42</text>
</svg>

### 4.6 Damage-mitigation chart (Vega-Lite)

For healer/tank specs: a line of incoming damage with shaded "this defensive was up" bands.

<svg xmlns="http://www.w3.org/2000/svg" width="720" height="220" viewBox="0 0 720 220">
  <rect width="720" height="220" fill="#1a1a1a"/>
  <text x="20" y="26" fill="#ddd" font-family="sans-serif" font-size="12" font-weight="bold">Incoming damage (HP%)</text>
  <!-- axes -->
  <line x1="60" y1="180" x2="690" y2="180" stroke="#444"/>
  <line x1="60" y1="40" x2="60" y2="180" stroke="#444"/>
  <text x="56" y="46" fill="#666" font-family="sans-serif" font-size="9" text-anchor="end">100%</text>
  <text x="56" y="184" fill="#666" font-family="sans-serif" font-size="9" text-anchor="end">0%</text>
  <!-- defensive band 1 -->
  <rect x="160" y="40" width="80" height="140" fill="#2090c0" fill-opacity="0.15"/>
  <text x="200" y="56" fill="#2090c0" font-family="sans-serif" font-size="10" text-anchor="middle" font-weight="bold">Astral Shift</text>
  <!-- defensive band 2 -->
  <rect x="380" y="40" width="60" height="140" fill="#4ec04e" fill-opacity="0.15"/>
  <text x="410" y="56" fill="#4ec04e" font-family="sans-serif" font-size="10" text-anchor="middle" font-weight="bold">Stone Bulwark</text>
  <!-- defensive band 3 (mediocre) -->
  <rect x="540" y="40" width="60" height="140" fill="#dd5533" fill-opacity="0.18"/>
  <text x="570" y="56" fill="#dd5533" font-family="sans-serif" font-size="10" text-anchor="middle" font-weight="bold">Healing Stream</text>
  <!-- damage line -->
  <polyline points="60,80 100,90 140,110 180,160 220,150 260,140 300,80 340,100 380,140 420,170 460,150 500,100 540,150 580,170 620,140 660,90 690,110"
    fill="none" stroke="#dd5533" stroke-width="2"/>
  <!-- legend -->
  <g transform="translate(60,202)" font-family="sans-serif" font-size="10" fill="#aaa">
    <rect x="0" y="-9" width="10" height="10" fill="#dd5533"/><text x="14" y="0">HP%</text>
    <rect x="60" y="-9" width="10" height="10" fill="#2090c0" fill-opacity="0.4"/><text x="74" y="0">Defensive active (color = QualitativePerformance of that use)</text>
  </g>
</svg>

---

## 5. Cooldown analysis

### 5.1 SpellUsageSubSection — full layout

This is the single most important composite — the entire MajorCooldown UI is built from it.
Two-column ExplanationRow: prose on the left, a stacked stack on the right (above-text →
PerformanceBoxRow → cast-detail panel → below-text).

<svg xmlns="http://www.w3.org/2000/svg" width="720" height="480" viewBox="0 0 720 480">
  <rect width="720" height="480" fill="#1a1a1a"/>
  <!-- SubSection title -->
  <text x="10" y="26" fill="#ddd" font-family="sans-serif" font-size="13" font-weight="bold">Stormkeeper</text>
  <!-- ExplanationRow -->
  <!-- LEFT: explanation -->
  <rect x="10" y="38" width="207" height="430" fill="#222" rx="4"/>
  <text x="20" y="60" fill="#ddd" font-family="sans-serif" font-size="11" font-weight="bold">Explanation</text>
  <text x="20" y="80" fill="#aaa" font-family="sans-serif" font-size="10">Stormkeeper should be cast</text>
  <text x="20" y="94" fill="#aaa" font-family="sans-serif" font-size="10">on cooldown unless saving</text>
  <text x="20" y="108" fill="#aaa" font-family="sans-serif" font-size="10">for a burst window.</text>
  <text x="20" y="132" fill="#aaa" font-family="sans-serif" font-size="10">Each cast empowers your next</text>
  <text x="20" y="146" fill="#aaa" font-family="sans-serif" font-size="10">two Lightning Bolts.</text>
  <text x="20" y="172" fill="#aaa" font-family="sans-serif" font-size="10">Pair with Ascendance whenever</text>
  <text x="20" y="186" fill="#aaa" font-family="sans-serif" font-size="10">possible.</text>
  <!-- RIGHT: SpellUsageDetailsContainer -->
  <rect x="223" y="38" width="487" height="430" fill="#262626" rx="4"/>
  <!-- (1) abovePerformanceDetails -->
  <text x="233" y="62" fill="#888" font-family="sans-serif" font-size="10" font-style="italic">[ optional abovePerformanceDetails — e.g. stats line ]</text>
  <text x="233" y="78" fill="#ddd" font-family="sans-serif" font-size="11">8 of 9 possible casts &nbsp; · &nbsp; 92% on-time</text>
  <!-- (2) Cast Breakdown header -->
  <text x="233" y="108" fill="#ddd" font-family="sans-serif" font-size="11" font-weight="bold">Cast Breakdown</text>
  <text x="343" y="108" fill="#888" font-family="sans-serif" font-size="9">— click a box to view details</text>
  <!-- (3) PerformanceBoxRow -->
  <g transform="translate(233,118)">
    <rect x="0"   y="0" width="20" height="20" fill="#4ec04e"/>
    <rect x="22"  y="0" width="20" height="20" fill="#2090c0"/>
    <rect x="44"  y="0" width="20" height="20" fill="#4ec04e"/>
    <rect x="66"  y="0" width="20" height="20" fill="#ffc84a"/>
    <rect x="88"  y="0" width="20" height="20" fill="#4ec04e"/>
    <rect x="110" y="0" width="20" height="20" fill="#4ec04e" stroke="#fff" stroke-width="2"/>
    <rect x="132" y="0" width="20" height="20" fill="#2090c0"/>
    <rect x="154" y="0" width="20" height="20" fill="#ac1f39"/>
    <rect x="176" y="0" width="20" height="20" fill="#4ec04e"/>
    <rect x="198" y="0" width="20" height="20" fill="#ac1f39"/>
  </g>
  <!-- (4) SpellUseDetails for selected box -->
  <rect x="233" y="158" width="467" height="270" fill="#1c1c1c" rx="3"/>
  <text x="243" y="180" fill="#bbb" font-family="sans-serif" font-size="11" font-weight="bold">Time</text>
  <text x="293" y="180" fill="#ddd" font-family="sans-serif" font-size="11">01:34.2</text>
  <text x="243" y="206" fill="#bbb" font-family="sans-serif" font-size="11" font-weight="bold">Perf.</text>
  <text x="293" y="206" fill="#ddd" font-family="sans-serif" font-size="11" font-weight="bold">Explanation</text>
  <line x1="243" y1="216" x2="690" y2="216" stroke="#333"/>
  <!-- checklist items -->
  <g transform="translate(243,230)">
    <path d="M0,4 L7,11 L18,-2" stroke="#4ec04e" stroke-width="2.5" fill="none" stroke-linecap="round" stroke-linejoin="round"/>
    <text x="50" y="9" fill="#ddd" font-family="sans-serif" font-size="11">Cast within 5 s of CD coming up</text>
  </g>
  <g transform="translate(243,256)">
    <circle cx="9" cy="6" r="9" fill="none" stroke="#2090c0" stroke-width="2"/>
    <path d="M4,6 L8,11 L15,1" stroke="#2090c0" stroke-width="2" fill="none" stroke-linecap="round" stroke-linejoin="round"/>
    <text x="50" y="11" fill="#ddd" font-family="sans-serif" font-size="11">Paired with Ascendance window</text>
  </g>
  <g transform="translate(243,282)">
    <text x="9" y="14" fill="#ffc84a" text-anchor="middle" font-family="sans-serif" font-size="18" font-weight="bold">*</text>
    <text x="50" y="11" fill="#ddd" font-family="sans-serif" font-size="11">Used 2/2 empowered Lightning Bolt charges</text>
    <text x="50" y="25" fill="#888" font-family="sans-serif" font-size="10">(one bolt was cast outside Stormkeeper buff window)</text>
  </g>
  <!-- extraDetails rounded sub-panel -->
  <rect x="243" y="332" width="447" height="84" fill="#262626" rx="3" stroke="#333"/>
  <text x="253" y="352" fill="#ddd" font-family="sans-serif" font-size="11" font-weight="bold">Extra Details</text>
  <text x="253" y="372" fill="#aaa" font-family="sans-serif" font-size="10">Total damage attributed: 1.8M (12% of overall).</text>
  <text x="253" y="388" fill="#aaa" font-family="sans-serif" font-size="10">Buffs active: Ascendance, Master of the Elements.</text>
  <!-- annotation arrows on left explanation -->
  <text x="20" y="240" fill="#555" font-family="sans-serif" font-size="10" font-style="italic">(Toggle "Hide Good Casts"</text>
  <text x="20" y="254" fill="#555" font-family="sans-serif" font-size="10" font-style="italic">filters the box row to only</text>
  <text x="20" y="268" fill="#555" font-family="sans-serif" font-size="10" font-style="italic">show non-Good casts.)</text>
  <text x="20" y="310" fill="#555" font-family="sans-serif" font-size="10" font-style="italic">If a click lands on a synthetic</text>
  <text x="20" y="324" fill="#555" font-family="sans-serif" font-size="10" font-style="italic">"missed cast" box, the right</text>
  <text x="20" y="338" fill="#555" font-family="sans-serif" font-size="10" font-style="italic">side shows a "you skipped a</text>
  <text x="20" y="352" fill="#555" font-family="sans-serif" font-size="10" font-style="italic">cast" message instead.</text>
</svg>

### 5.2 CooldownExpandable — accordion summary of one cooldown use

Compact collapsible row used in some lists; click to expand a per-cast breakdown.

<svg xmlns="http://www.w3.org/2000/svg" width="720" height="280" viewBox="0 0 720 280">
  <rect width="720" height="280" fill="#1a1a1a"/>
  <!-- collapsed row 1 -->
  <rect x="10" y="14" width="700" height="38" rx="4" fill="#1f1f1f" stroke="#2090c0" stroke-width="2"/>
  <text x="25" y="38" fill="#ddd" font-family="sans-serif" font-size="13">▶</text>
  <rect x="48" y="22" width="20" height="22" fill="#7a5fff" rx="2"/>
  <text x="78" y="38" fill="#fff" font-family="sans-serif" font-size="12">Stormkeeper @ 00:18.1</text>
  <text x="280" y="38" fill="#888" font-family="sans-serif" font-size="11">— 3 of 3 checks · 1.8M dmg attributed</text>
  <text x="680" y="38" fill="#2090c0" font-family="sans-serif" font-size="11" font-weight="bold" text-anchor="end">Perfect</text>
  <!-- expanded row -->
  <rect x="10" y="60" width="700" height="180" rx="4" fill="#1f1f1f" stroke="#ffc84a" stroke-width="2"/>
  <text x="25" y="84" fill="#ddd" font-family="sans-serif" font-size="13">▼</text>
  <rect x="48" y="68" width="20" height="22" fill="#7a5fff" rx="2"/>
  <text x="78" y="84" fill="#fff" font-family="sans-serif" font-size="12">Stormkeeper @ 01:34.2</text>
  <text x="680" y="84" fill="#ffc84a" font-family="sans-serif" font-size="11" font-weight="bold" text-anchor="end">Ok</text>
  <line x1="10" y1="98" x2="710" y2="98" stroke="#333"/>
  <!-- expanded body -->
  <g font-family="sans-serif" transform="translate(40,116)">
    <path d="M0,4 L7,11 L18,-2" stroke="#4ec04e" stroke-width="2.5" fill="none" stroke-linecap="round" stroke-linejoin="round"/>
    <text x="40" y="9" fill="#ddd" font-size="11">Cast within 5 s of CD coming up</text>
    <g transform="translate(0,26)">
      <text x="9" y="14" fill="#ffc84a" text-anchor="middle" font-size="16" font-weight="bold">*</text>
      <text x="40" y="11" fill="#ddd" font-size="11">Used 2/2 empowered LBs</text>
      <text x="40" y="25" fill="#888" font-size="10">but one was cast outside the buff window</text>
    </g>
    <g transform="translate(0,68)">
      <path d="M2,2 L18,18 M18,2 L2,18" stroke="#ac1f39" stroke-width="2.5" stroke-linecap="round"/>
      <text x="40" y="13" fill="#ddd" font-size="11">Paired with Ascendance window</text>
      <text x="40" y="27" fill="#888" font-size="10">Ascendance was on cooldown when SK was used</text>
    </g>
  </g>
  <!-- collapsed row 3 -->
  <rect x="10" y="248" width="700" height="22" rx="4" fill="#1f1f1f" stroke="#ac1f39" stroke-width="2"/>
  <text x="25" y="264" fill="#ddd" font-family="sans-serif" font-size="11">▶ Stormkeeper @ 02:48.0 &nbsp; — 1 of 3 checks</text>
  <text x="680" y="264" fill="#ac1f39" font-family="sans-serif" font-size="10" font-weight="bold" text-anchor="end">Fail</text>
</svg>

### 5.3 Mitigation / MitigationSegment — defensive cooldown chart

For MajorDefensive analyzers: each segment of a defensive's window is shaded by how much it
actually mitigated. Hover for the per-event damage breakdown.

<svg xmlns="http://www.w3.org/2000/svg" width="720" height="200" viewBox="0 0 720 200">
  <rect width="720" height="200" fill="#1a1a1a"/>
  <text x="20" y="28" fill="#ddd" font-family="sans-serif" font-size="12" font-weight="bold">Astral Shift — uses</text>
  <!-- Use 1 (good) -->
  <text x="20" y="58" fill="#aaa" font-family="sans-serif" font-size="10">00:18 → 00:26</text>
  <rect x="120" y="46" width="200" height="18" fill="#262626" rx="2"/>
  <rect x="120" y="46" width="40"  height="18" fill="#4ec04e"/>
  <rect x="160" y="46" width="60"  height="18" fill="#2090c0"/>
  <rect x="220" y="46" width="80"  height="18" fill="#4ec04e"/>
  <text x="330" y="60" fill="#4ec04e" font-family="sans-serif" font-size="11" font-weight="bold">Good — 412k mitigated</text>
  <!-- Use 2 (ok) -->
  <text x="20" y="98" fill="#aaa" font-family="sans-serif" font-size="10">01:42 → 01:50</text>
  <rect x="120" y="86" width="200" height="18" fill="#262626" rx="2"/>
  <rect x="120" y="86" width="60"  height="18" fill="#dd5533"/>
  <rect x="180" y="86" width="80"  height="18" fill="#ffc84a"/>
  <rect x="260" y="86" width="40"  height="18" fill="#696864"/>
  <text x="330" y="100" fill="#ffc84a" font-family="sans-serif" font-size="11" font-weight="bold">Ok — 180k mitigated (idled)</text>
  <!-- Use 3 (bad) -->
  <text x="20" y="138" fill="#aaa" font-family="sans-serif" font-size="10">03:05 → 03:13</text>
  <rect x="120" y="126" width="200" height="18" fill="#262626" rx="2"/>
  <rect x="120" y="126" width="200" height="18" fill="#696864"/>
  <text x="330" y="140" fill="#ac1f39" font-family="sans-serif" font-size="11" font-weight="bold">Fail — popped pre-emptively, 0 damage taken</text>
  <!-- legend -->
  <g transform="translate(20,176)" font-family="sans-serif" font-size="10" fill="#aaa">
    <rect x="0" y="-9" width="10" height="10" fill="#4ec04e"/><text x="14" y="0">heavy hit mitigated</text>
    <rect x="140" y="-9" width="10" height="10" fill="#dd5533"/><text x="154" y="0">partial mitigation</text>
    <rect x="280" y="-9" width="10" height="10" fill="#696864"/><text x="294" y="0">no damage taken (segment wasted)</text>
  </g>
</svg>

---

## 6. Auxiliary primitives

### 6.1 TipBox and PerformanceTipBox

Boxed callout for additional context. `PerformanceTipBox` colors the left border by grade.

<svg xmlns="http://www.w3.org/2000/svg" width="720" height="220" viewBox="0 0 720 220">
  <rect width="720" height="220" fill="#1a1a1a"/>
  <!-- TipBox -->
  <rect x="10" y="14" width="700" height="56" rx="4" fill="#1f1f1f" stroke="#444"/>
  <text x="28" y="40" fill="#ffd34a" font-family="sans-serif" font-size="14" font-weight="bold">i</text>
  <text x="50" y="34" fill="#ddd" font-family="sans-serif" font-size="11" font-weight="bold">TipBox — neutral tip</text>
  <text x="50" y="54" fill="#aaa" font-family="sans-serif" font-size="11">"Hold this charge through movement phases — the burn window is more valuable than the residual."</text>
  <!-- Perfect tip -->
  <rect x="10" y="84" width="700" height="38" rx="4" fill="#1f1f1f"/>
  <rect x="10" y="84" width="6" height="38" fill="#2090c0"/>
  <text x="28" y="108" fill="#ddd" font-family="sans-serif" font-size="11">PerformanceTipBox · Perfect &nbsp; — "You held this through every immune window. Top tier."</text>
  <!-- Good tip -->
  <rect x="10" y="132" width="700" height="38" rx="4" fill="#1f1f1f"/>
  <rect x="10" y="132" width="6" height="38" fill="#4ec04e"/>
  <text x="28" y="156" fill="#ddd" font-family="sans-serif" font-size="11">PerformanceTipBox · Good &nbsp; &nbsp;— "Solid usage. One cast could have been earlier."</text>
  <!-- Bad tip -->
  <rect x="10" y="180" width="700" height="38" rx="4" fill="#1f1f1f"/>
  <rect x="10" y="180" width="6" height="38" fill="#ac1f39"/>
  <text x="28" y="204" fill="#ddd" font-family="sans-serif" font-size="11">PerformanceTipBox · Fail &nbsp; &nbsp;&nbsp;— "You died with this still off cooldown — pre-pop next time."</text>
</svg>

### 6.2 ProblemList — issue-by-issue triage view

One row per detected problem, ranked by severity. Click a row to jump to a timestamp.

<svg xmlns="http://www.w3.org/2000/svg" width="720" height="240" viewBox="0 0 720 240">
  <rect width="720" height="240" fill="#1a1a1a"/>
  <text x="20" y="30" fill="#fff" font-family="sans-serif" font-size="13" font-weight="bold">Problems</text>
  <text x="100" y="30" fill="#888" font-family="sans-serif" font-size="11">— 3 issues found</text>
  <!-- row 1 -->
  <rect x="10" y="44" width="700" height="56" rx="4" fill="#1f1f1f" stroke="#ac1f39"/>
  <path d="M30,68 L50,88 M50,68 L30,88" stroke="#ac1f39" stroke-width="3" stroke-linecap="round"/>
  <text x="68" y="72" fill="#ddd" font-family="sans-serif" font-size="12" font-weight="bold">Missed Stormkeeper at 02:48</text>
  <text x="68" y="90" fill="#aaa" font-family="sans-serif" font-size="11">Cooldown was up for 18 s before the fight ended.</text>
  <text x="690" y="78" fill="#ac1f39" font-family="sans-serif" font-size="11" font-weight="bold" text-anchor="end">High</text>
  <!-- row 2 -->
  <rect x="10" y="108" width="700" height="56" rx="4" fill="#1f1f1f" stroke="#ffc84a"/>
  <text x="40" y="148" fill="#ffc84a" text-anchor="middle" font-family="sans-serif" font-size="28" font-weight="bold">*</text>
  <text x="68" y="136" fill="#ddd" font-family="sans-serif" font-size="12" font-weight="bold">Wasted Maelstrom at 01:22</text>
  <text x="68" y="154" fill="#aaa" font-family="sans-serif" font-size="11">Capped for 4 s — should have spent on Earth Shock.</text>
  <text x="690" y="142" fill="#ffc84a" font-family="sans-serif" font-size="11" font-weight="bold" text-anchor="end">Medium</text>
  <!-- row 3 -->
  <rect x="10" y="172" width="700" height="56" rx="4" fill="#1f1f1f" stroke="#ffc84a"/>
  <text x="40" y="212" fill="#ffc84a" text-anchor="middle" font-family="sans-serif" font-size="28" font-weight="bold">*</text>
  <text x="68" y="200" fill="#ddd" font-family="sans-serif" font-size="12" font-weight="bold">Flame Shock dropped on adds at 03:14</text>
  <text x="68" y="218" fill="#aaa" font-family="sans-serif" font-size="11">DoT was inactive for 6 s during the cleave window.</text>
  <text x="690" y="206" fill="#ffc84a" font-family="sans-serif" font-size="11" font-weight="bold" text-anchor="end">Medium</text>
</svg>

### 6.3 APL — Action Priority List trace

Shows the recommended rotational priority alongside what the player actually did.
Row colored = mismatch.

<svg xmlns="http://www.w3.org/2000/svg" width="720" height="260" viewBox="0 0 720 260">
  <rect width="720" height="260" fill="#1a1a1a"/>
  <rect x="10" y="10" width="700" height="240" rx="8" fill="rgba(30,30,30,0.85)" stroke="#333"/>
  <text x="20" y="34" fill="#fff" font-family="sans-serif" font-size="13" font-weight="bold">APL Trace</text>
  <!-- header -->
  <g font-family="sans-serif" font-size="11" transform="translate(20,52)">
    <text x="0"   y="0" fill="#888">Time</text>
    <text x="80"  y="0" fill="#888">Cast</text>
    <text x="240" y="0" fill="#888">Recommended</text>
    <text x="430" y="0" fill="#888">Result</text>
    <line x1="0" y1="8" x2="660" y2="8" stroke="#333"/>
  </g>
  <g font-family="sans-serif" font-size="11" transform="translate(20,76)">
    <text x="0"   y="14" fill="#ddd">00:01.2</text>
    <text x="80"  y="14" fill="#ddd">Stormkeeper</text>
    <text x="240" y="14" fill="#ddd">Stormkeeper</text>
    <text x="430" y="14" fill="#4ec04e" font-weight="bold">✓ matched</text>
    <text x="0"   y="38" fill="#ddd">00:02.6</text>
    <text x="80"  y="38" fill="#ddd">Lightning Bolt</text>
    <text x="240" y="38" fill="#ddd">Lava Burst</text>
    <text x="430" y="38" fill="#ffc84a" font-weight="bold">≠ mismatch</text>
    <text x="540" y="38" fill="#888" font-style="italic">— LvB on CD</text>
    <text x="0"   y="62" fill="#ddd">00:04.2</text>
    <text x="80"  y="62" fill="#ddd">Lava Burst</text>
    <text x="240" y="62" fill="#ddd">Lava Burst</text>
    <text x="430" y="62" fill="#4ec04e" font-weight="bold">✓ matched</text>
    <text x="0"   y="86" fill="#ddd">00:05.8</text>
    <text x="80"  y="86" fill="#ddd">Earth Shock</text>
    <text x="240" y="86" fill="#ddd">Lightning Bolt</text>
    <text x="430" y="86" fill="#ac1f39" font-weight="bold">✗ wrong spender</text>
    <text x="540" y="86" fill="#888" font-style="italic">— ES not optimal here</text>
    <text x="0"   y="110" fill="#ddd">00:07.4</text>
    <text x="80"  y="110" fill="#ddd">Lightning Bolt</text>
    <text x="240" y="110" fill="#ddd">Lightning Bolt</text>
    <text x="430" y="110" fill="#4ec04e" font-weight="bold">✓ matched</text>
    <text x="0"   y="146" fill="#888" font-style="italic">…</text>
  </g>
  <!-- summary footer -->
  <line x1="20" y1="222" x2="690" y2="222" stroke="#2a2a2a"/>
  <text x="20" y="240" fill="#aaa" font-family="sans-serif" font-size="11">88% rotation match · 6 mismatches · 1 wrong spender</text>
</svg>

### 6.4 Preparation — pre-pull / consumables checklist

Static list of "did you bring X" checks before the pull.

<svg xmlns="http://www.w3.org/2000/svg" width="720" height="220" viewBox="0 0 720 220">
  <rect width="720" height="220" fill="#1a1a1a"/>
  <rect x="10" y="10" width="700" height="200" rx="8" fill="rgba(30,30,30,0.85)" stroke="#333"/>
  <text x="20" y="34" fill="#fff" font-family="sans-serif" font-size="13" font-weight="bold">Preparation</text>
  <g font-family="sans-serif" font-size="11" transform="translate(20,52)">
    <!-- 1 -->
    <path d="M0,4 L7,11 L18,-2" stroke="#4ec04e" stroke-width="2.5" fill="none" stroke-linecap="round" stroke-linejoin="round"/>
    <text x="32" y="9" fill="#ddd">Pre-potted (Tempered Potion)</text>
    <!-- 2 -->
    <g transform="translate(0,26)">
      <path d="M0,4 L7,11 L18,-2" stroke="#4ec04e" stroke-width="2.5" fill="none" stroke-linecap="round" stroke-linejoin="round"/>
      <text x="32" y="9" fill="#ddd">Flask: Flask of Tempered Swiftness</text>
    </g>
    <!-- 3 -->
    <g transform="translate(0,52)">
      <path d="M0,4 L7,11 L18,-2" stroke="#4ec04e" stroke-width="2.5" fill="none" stroke-linecap="round" stroke-linejoin="round"/>
      <text x="32" y="9" fill="#ddd">Food: Feast of the Divine Day</text>
    </g>
    <!-- 4 -->
    <g transform="translate(0,78)">
      <path d="M2,2 L18,18 M18,2 L2,18" stroke="#ac1f39" stroke-width="2.5" stroke-linecap="round"/>
      <text x="32" y="13" fill="#ddd">Augment rune missing</text>
    </g>
    <!-- 5 -->
    <g transform="translate(0,104)">
      <path d="M0,4 L7,11 L18,-2" stroke="#4ec04e" stroke-width="2.5" fill="none" stroke-linecap="round" stroke-linejoin="round"/>
      <text x="32" y="9" fill="#ddd">Weapon enchant: Authority of Storms</text>
    </g>
    <!-- 6 -->
    <g transform="translate(0,130)">
      <text x="9" y="14" fill="#ffc84a" text-anchor="middle" font-size="20" font-weight="bold">*</text>
      <text x="32" y="11" fill="#ddd">1 of 12 gem sockets unfilled</text>
    </g>
  </g>
</svg>

---

## 7. Whole-guide composition

How the primitives stack inside one real spec's guide. Schematic only — proportions are illustrative.

<svg xmlns="http://www.w3.org/2000/svg" width="720" height="780" viewBox="0 0 720 780">
  <rect width="720" height="780" fill="#1a1a1a"/>
  <!-- GuideContainer outline -->
  <text x="10" y="20" fill="#666" font-family="sans-serif" font-size="10" font-style="italic">GuideContainer (root, flex-column, gap)</text>
  <!-- Section 1: Preparation -->
  <rect x="10" y="30" width="700" height="80" rx="4" fill="#1f1f1f" stroke="#3a3a3a"/>
  <rect x="10" y="30" width="700" height="28" rx="4" fill="#2a2a1a"/>
  <text x="22" y="50" fill="#ffd34a" font-family="sans-serif" font-size="12" font-weight="bold">Preparation</text>
  <text x="685" y="50" fill="#ffd34a" text-anchor="end" font-family="sans-serif" font-size="12">▼</text>
  <text x="22" y="78" fill="#888" font-family="sans-serif" font-size="10">Preparation primitive — flask / food / pots / enchants / gems checks</text>
  <!-- Section 2: Foundations -->
  <rect x="10" y="120" width="700" height="160" rx="4" fill="#1f1f1f" stroke="#3a3a3a"/>
  <rect x="10" y="120" width="700" height="28" rx="4" fill="#2a2a1a"/>
  <text x="22" y="140" fill="#ffd34a" font-family="sans-serif" font-size="12" font-weight="bold">Foundations</text>
  <text x="685" y="140" fill="#ffd34a" text-anchor="end" font-family="sans-serif" font-size="12">▼</text>
  <text x="22" y="166" fill="#bbb" font-family="sans-serif" font-size="10" font-weight="bold">SubSection: Downtime</text>
  <rect x="22" y="172" width="676" height="20" fill="#262626" rx="2"/>
  <rect x="22" y="172" width="600" height="20" fill="#4ec04e"/>
  <text x="630" y="186" fill="#aaa" font-family="sans-serif" font-size="10">7%</text>
  <text x="22" y="216" fill="#bbb" font-family="sans-serif" font-size="10" font-weight="bold">SubSection: Cooldown availability</text>
  <rect x="22" y="222" width="676" height="20" fill="#262626" rx="2"/>
  <rect x="22" y="222" width="540" height="20" fill="#ffc84a"/>
  <text x="22" y="262" fill="#888" font-family="sans-serif" font-size="10">…</text>
  <!-- Section 3: Core skills -->
  <rect x="10" y="290" width="700" height="240" rx="4" fill="#1f1f1f" stroke="#3a3a3a"/>
  <rect x="10" y="290" width="700" height="28" rx="4" fill="#2a2a1a"/>
  <text x="22" y="310" fill="#ffd34a" font-family="sans-serif" font-size="12" font-weight="bold">Core Skills</text>
  <text x="685" y="310" fill="#ffd34a" text-anchor="end" font-family="sans-serif" font-size="12">▼</text>
  <text x="22" y="338" fill="#bbb" font-family="sans-serif" font-size="11" font-weight="bold">Lava Burst</text>
  <!-- Two-col -->
  <rect x="22" y="346" width="200" height="80" fill="#222" rx="3"/>
  <text x="32" y="362" fill="#888" font-family="sans-serif" font-size="9">Explanation</text>
  <text x="32" y="378" fill="#666" font-family="sans-serif" font-size="9">prose…</text>
  <rect x="228" y="346" width="470" height="80" fill="#262626" rx="3"/>
  <text x="238" y="362" fill="#888" font-family="sans-serif" font-size="9">Cast Breakdown</text>
  <g transform="translate(238,372)">
    <rect x="0"   y="0" width="12" height="12" fill="#4ec04e"/>
    <rect x="14"  y="0" width="12" height="12" fill="#4ec04e"/>
    <rect x="28"  y="0" width="12" height="12" fill="#2090c0"/>
    <rect x="42"  y="0" width="12" height="12" fill="#ffc84a"/>
    <rect x="56"  y="0" width="12" height="12" fill="#4ec04e"/>
    <rect x="70"  y="0" width="12" height="12" fill="#ac1f39"/>
    <rect x="84"  y="0" width="12" height="12" fill="#4ec04e"/>
    <rect x="98"  y="0" width="12" height="12" fill="#4ec04e"/>
    <rect x="112" y="0" width="12" height="12" fill="#2090c0"/>
    <rect x="126" y="0" width="12" height="12" fill="#4ec04e"/>
    <rect x="140" y="0" width="12" height="12" fill="#ffc84a"/>
    <rect x="154" y="0" width="12" height="12" fill="#4ec04e"/>
  </g>
  <text x="238" y="408" fill="#666" font-family="sans-serif" font-size="9">SpellUseDetails area expands when a box is clicked</text>
  <!-- second SubSection -->
  <text x="22" y="448" fill="#bbb" font-family="sans-serif" font-size="11" font-weight="bold">Earth Shock</text>
  <rect x="22" y="456" width="200" height="60" fill="#222" rx="3"/>
  <rect x="228" y="456" width="470" height="60" fill="#262626" rx="3"/>
  <g transform="translate(238,478)">
    <rect x="0"   y="0" width="12" height="12" fill="#4ec04e"/>
    <rect x="14"  y="0" width="12" height="12" fill="#2090c0"/>
    <rect x="28"  y="0" width="12" height="12" fill="#4ec04e"/>
    <rect x="42"  y="0" width="12" height="12" fill="#4ec04e"/>
    <rect x="56"  y="0" width="12" height="12" fill="#ffc84a"/>
    <rect x="70"  y="0" width="12" height="12" fill="#4ec04e"/>
    <rect x="84"  y="0" width="12" height="12" fill="#ac1f39"/>
    <rect x="98"  y="0" width="12" height="12" fill="#4ec04e"/>
  </g>
  <!-- Section 4: Cooldowns -->
  <rect x="10" y="540" width="700" height="160" rx="4" fill="#1f1f1f" stroke="#3a3a3a"/>
  <rect x="10" y="540" width="700" height="28" rx="4" fill="#2a2a1a"/>
  <text x="22" y="560" fill="#ffd34a" font-family="sans-serif" font-size="12" font-weight="bold">Rotation &amp; Cooldowns</text>
  <text x="685" y="560" fill="#ffd34a" text-anchor="end" font-family="sans-serif" font-size="12">▼</text>
  <text x="22" y="586" fill="#bbb" font-family="sans-serif" font-size="11" font-weight="bold">Stormkeeper · Ascendance · Primordial Wave</text>
  <text x="22" y="606" fill="#888" font-family="sans-serif" font-size="10">Each rendered via SpellUsageSubSection (one per major cooldown)</text>
  <text x="22" y="634" fill="#888" font-family="sans-serif" font-size="10">— talent-gated: Primordial Wave only shown if hasTalent(PRIMAL_WAVE)</text>
  <text x="22" y="666" fill="#666" font-family="sans-serif" font-size="10" font-style="italic">…</text>
  <!-- Section 5: Defensives -->
  <rect x="10" y="710" width="700" height="60" rx="4" fill="#1f1f1f" stroke="#3a3a3a"/>
  <rect x="10" y="710" width="700" height="28" rx="4" fill="#2a2a1a"/>
  <text x="22" y="730" fill="#ffd34a" font-family="sans-serif" font-size="12" font-weight="bold">Defensives</text>
  <text x="685" y="730" fill="#ffd34a" text-anchor="end" font-family="sans-serif" font-size="12">▼</text>
  <text x="22" y="758" fill="#888" font-family="sans-serif" font-size="10">Mitigation chart + per-use breakdown per defensive analyzer</text>
</svg>

---

## 8. Mapping mockups → component catalog

| Section | Mockup | Catalog entry |
|---|---|---|
| 0.1 | PerformanceMark / PassFailCheckmark | 0.1 QualitativePerformance |
| 0.2 | BoxRowEntry | 0.2 Box-row entry |
| 0.3 | Theme containers | 0.3 Theme containers |
| 1 | Section + SubSection | 1.1 / 1.2 |
| 1 | ExplanationRow | 1 Foundation, used everywhere |
| 1 | GuideSection | 1.4 |
| 2 | GuideDataWrapper | 2.1 |
| 2 | StatCard | 2.2 |
| 2 | StatsGrid | 2.3 |
| 2 | PerfBadgeGrid | 2.4 |
| 3 | CastSummary | 3.1 |
| 3 | CastOverview | 3.2 |
| 3 | CastDetail | 3.3 |
| 3 | CastSequence | 3.4 |
| 4 | PerformanceBoxRow | 4.1 |
| 4 | StackedBar | 4.2 |
| 4 | GradiatedPerformanceBar | 4.3 |
| 4 | PassFailBar | 4.4 |
| 4 | BuffUptimeBar | 4.5 |
| 4 | Damage-mitigation chart | 4.6 |
| 5 | SpellUsageSubSection | 5.1 (the centerpiece) |
| 5 | CooldownExpandable | 5.2 |
| 5 | Mitigation / MitigationSegment | 5.3 |
| 6 | TipBox / PerformanceTipBox | 6 Other primitives |
| 6 | ProblemList | 6 |
| 6 | APL | 6 |
| 6 | Preparation | 6 |
| 7 | Whole-guide composition | 7 Composition patterns from real specs |

The mockups omit purely structural pieces (`GuideContainer`, `HideExplanationsToggle`,
`HideGoodCastsToggle`) and the foundation cross-spec helpers (`FoundationDowntimeSection` etc.),
since those are layout/aggregation wrappers around the primitives already shown above.
