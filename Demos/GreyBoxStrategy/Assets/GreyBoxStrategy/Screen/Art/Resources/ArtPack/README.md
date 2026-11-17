# Painted Realm: the art pack

Pictures made ahead of time on the team's asset server (`tools/comfyui/comfy.py run image`, Z-Image Turbo,
8 steps) and chosen by looking at two or more candidates each, then trimmed by 2.5% a side and resized. `ArtCatalogue.cs` says which picture a ruler,
a general or an event shows. No picture shows a real person, a named artist's style or any lettering.

To remake one: `python tools/comfyui/comfy.py run image --set "prompt=<its prompt>" --set seed=<its seed>
--set width=<w> --set height=<h>`, then `python Demos/GreyBoxStrategy/ArtSource/art-pack/build_pack.py
<folder>` resizes the chosen candidates into this folder. The choices are in `ArtSource/art-pack/pack.json`.

Every prompt is the style line, then the kind's line, then the subject.

- **Style:** Painted illustration in the manner of a late-medieval illuminated chronicle, tempera and gouache, muted earth colours, ochre, umber, madder red and slate blue, small gilt accents, soft even light, fine brushwork. The painting fills the whole picture to its edges. No text, no lettering, no border, no frame, no modern objects.
- **A portrait** (768 x 896): Bust portrait, head and shoulders, three-quarter view, centred, plain dark brown background, an invented person.
- **An event** (1024 x 576): A wide scene seen from a middle distance, small figures.

## Rulers

| File | Seed | Subject |
|---|---|---|
| `portraits/warmonger-1.jpg` | 11001 | A scarred, broad warlord in his forties with a black beard, a mail coat, a wolf-fur mantle and an iron circlet, glaring. |
| `portraits/warmonger-2.jpg` | 11002 | A fierce warrior queen in her thirties with braided red hair, a steel gorget, a crimson cloak and a thin gold circlet, a hard stare. |
| `portraits/warmonger-3.jpg` | 11003 | An old grizzled war-king with a grey beard, a scar through one eyebrow, a dented breastplate, a bear-fur collar and a plain iron crown. |
| `portraits/builder-1.jpg` | 11004 | A calm, thoughtful king in his fifties with a trimmed grey beard, a plain blue wool robe and a simple gold crown. |
| `portraits/builder-2.jpg` | 33002 | A composed queen in her forties with a linen veil under a modest circlet, a plain green gown and a simple gold chain, a patient look, the dark background reaching every edge. |
| `portraits/builder-3.jpg` | 11006 | A young, earnest prince, clean-shaven with brown hair, a brown doublet and a small coronet, a serious look. |
| `portraits/opportunist-1.jpg` | 11007 | A thin, sly lord with narrow eyes and a faint smile, a dark hooded cloak thrown back and a silver chain. |
| `portraits/opportunist-2.jpg` | 11008 | A cold, elegant duchess, pale with dark hair pulled back, a black velvet gown with a high collar and a small silver coronet, one eyebrow raised. |
| `portraits/opportunist-3.jpg` | 11009 | A lean, bald old king with a hooked nose, a fur-trimmed dark green robe and a thin crown, a watchful sideways look. |
| `portraits/merchant-1.jpg` | 22010 | A stout merchant prince with a round face, a velvet cap with a feather, a fur collar and a heavy gold chain, a pleased smile. |
| `portraits/merchant-2.jpg` | 11011 | A shrewd merchant queen in her fifties with an embroidered headdress, pearls and a saffron brocade gown, a knowing half smile. |
| `portraits/merchant-3.jpg` | 11012 | A smooth young merchant lord with a neat dark beard, a red cap and a brocade robe, weighing the viewer with his eyes. |
| `portraits/proud-1.jpg` | 11013 | A haughty king with his chin raised, a pointed dark beard, a tall gold crown and an ermine mantle. |
| `portraits/proud-2.jpg` | 33001 | A proud, stern queen with a tall crown over a white veil and a deep red gown, a plain gold band at the neck with no pattern on it. |
| `portraits/proud-3.jpg` | 22015 | A stiff old king with white hair and a long face, a heavy crown and a purple mantle, a disdainful look. |
| `portraits/you-1.jpg` | 11016 | A young king with an open, steady face, short brown hair, a plain gold crown and a slate-blue cloak. |
| `portraits/you-2.jpg` | 22017 | A young queen with an open, steady face, dark braided hair, a plain gold crown and a green cloak. |
| `portraits/you-3.jpg` | 11018 | A weathered king in his fifties with a short grey beard, a simple circlet and a grey-brown cloak, a level look. |
| `portraits/you-4.jpg` | 22019 | A queen in her fifties with silver-streaked hair, a simple circlet and a wine-red cloak, a level look. |

## Generals

| File | Seed | Subject |
|---|---|---|
| `portraits/captain-m1.jpg` | 22020 | A weathered veteran commander with a broken nose and cropped grey hair, a steel gorget over a padded coat, bare-headed. |
| `portraits/captain-m2.jpg` | 22021 | A bald captain with an eye patch and a short beard, a mail coat and a dark cloak. |
| `portraits/captain-m3.jpg` | 11022 | A young knight with fair hair and an eager face, bright plate armour at the shoulders, bare-headed. |
| `portraits/captain-m4.jpg` | 22023 | A dark-skinned commander with a short grey beard and calm eyes, a brigandine with brass studs and a red sash. |
| `portraits/captain-m5.jpg` | 11024 | A stout captain with a big moustache and ruddy cheeks, a kettle helm pushed back and a leather jerkin. |
| `portraits/captain-m6.jpg` | 22025 | A gaunt, hawk-faced marshal with long dark hair tied back, black plate armour at the shoulders and a grey cloak. |
| `portraits/captain-w1.jpg` | 11026 | A stern woman commander with cropped grey hair and a scar on her cheek, a plate gorget over a padded coat, bare-headed. |
| `portraits/captain-w2.jpg` | 11027 | A young woman rider with black braided hair and a quick smile, a leather brigandine and a green scarf. |
| `portraits/captain-w3.jpg` | 11028 | A broad-shouldered woman warrior with a blonde braid, a mail coat and a fur collar, a hard look. |
| `portraits/captain-w4.jpg` | 11029 | A calm woman archer captain with brown skin and a dark hood thrown back, a leather jerkin, a quiver strap across her chest. |
| `portraits/captain-w5.jpg` | 11030 | A tall woman knight with auburn hair tied back, a mail coif pushed back on her shoulders and plate armour, bare-headed. |
| `portraits/captain-w6.jpg` | 11031 | An old woman siege master with a weathered face and white hair under a linen cap, a fur collar and a heavy leather apron. |

## Events

| File | Seed | Subject |
|---|---|---|
| `events/windfall.jpg` | 22032 | A castle hall where a steward lifts the lid of a chest full of gold coins, sunlight falling through a narrow window. |
| `events/bad_harvest.jpg` | 11033 | A withered wheat field under a pale sky, peasants standing among the dry stalks with empty baskets. |
| `events/plague.jpg` | 22034 | A soldiers' camp of patched tents, sick men lying under blankets, a monk tending one of them, smoke rising from small fires. |
| `events/volunteers.jpg` | 22035 | A line of villagers with spears and pitchforks walking up a road to a castle gate hung with plain banners. |
| `events/zeal.jpg` | 11036 | A crowd with raised plain banners gathered before a small stone chapel on a hill, golden evening light. |
| `events/unrest.jpg` | 11037 | An angry crowd with torches in a town market square at dusk, before the closed doors of a hall. |
| `events/stronghold.jpg` | 22038 | Masons on wooden scaffolding raising a stone castle wall, a treadwheel crane lifting a block. |
| `events/walls_crumble.jpg` | 22039 | A breached, crumbling castle wall with fallen stones in the grass, ivy on it, crows overhead, nobody about. |
| `events/good_harvest.jpg` | 22040 | A golden wheat field with reapers at work and carts loaded high with sheaves, a clear sky. |
| `events/bandits.jpg` | 22041 | Masked riders bursting from the trees upon a merchant's cart on a forest road. |
| `events/sellswords.jpg` | 11042 | A band of rough mercenaries in mismatched armour standing before a ruler's striped tent, their captain holding out an open hand. |
| `events/petitioners.jpg` | 22043 | Ragged peasants kneeling on the stone floor of a throne hall before a dais, guards at the pillars. |
| `events/relic.jpg` | 11044 | Workers in a pit beside a ruined chapel lifting out a small golden casket, a shaft of light falling on it. |
| `events/muster.jpg` | 22045 | On one side a field of ripe wheat with reapers, on the other men taking spears from a cart beside a plain banner. |
| `events/engineers.jpg` | 11046 | Master masons with compasses and plumb lines at a trestle table before a town wall, pointing at the wall. |
| `events/fever_camps.jpg` | 11047 | An army camp at night, tents closed, a single fire, soldiers wrapped in blankets, a guard standing apart. |
| `events/pretender.jpg` | 11048 | A young man in a crown too large for him standing on a cart before a cheering village crowd. |
| `events/pretender_rising.jpg` | 11049 | Armed rebels with scythes and spears under a ragged plain banner marching out of a forest toward a walled town. |
| `events/deep_vein.jpg` | 11050 | Miners with candles deep in a rock tunnel, a glittering vein of ore running through the wall beside them. |
| `events/ration.jpg` | 11051 | Inside a granary in winter, a steward measuring grain from sacks to a line of people in heavy cloaks, snow at the open door. |
| `events/rich_vein.jpg` | 22052 | A mine mouth in a hillside, miners carrying out baskets of shining ore to waiting carts. |
| `events/flood.jpg` | 11053 | A river village half under brown water, people on the thatched roofs and in small boats. |
| `events/pilgrims.jpg` | 22054 | A line of pilgrims in cloaks with staffs walking a winding road up to a shrine on a hilltop. |
| `events/omen.jpg` | 22055 | A comet with a long tail over a walled town at night, small figures on the walls pointing up. |
| `events/end_tale.jpg` | 11056 | An old scribe at a desk by candlelight closing a great leather-bound book, dawn at the window. |
| `events/tale_begins.jpg` | 22057 | A lone rider on a road at dawn approaching a distant walled town, long shadows, birds rising. |
| `events/long_winter.jpg` | 11058 | A snowbound village under a grey sky, thin smoke from the chimneys, people dragging a sledge of firewood. |
