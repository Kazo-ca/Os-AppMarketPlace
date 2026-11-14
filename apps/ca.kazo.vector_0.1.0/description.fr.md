# Kazo Vector

Une app de dessin vectoriel pour Kazo OS : logos, illustrations, icônes, affiches et schémas. Kazo
Vector est basé sur [VectorCraft](https://github.com/storytold/vectorcraft) de l'équipe ArtCraft,
une app d'illustration vectorielle libre écrite en Rust, qui tourne ici dans une fenêtre de Kazo OS.

## Dessiner

- Plume, crayon, outil courbe et outils de formes, avec de vrais points d'ancrage et poignées de
  Bézier.
- Opérations exactes sur les tracés (réunir, soustraire, intersection, diviser) et un outil de
  construction de formes.
- Fonds, contours, dégradés, nuanciers, transparence, masques d'écrêtage et effets en direct comme
  les lueurs et les ombres.
- Texte sur une ligne, dans un cadre ou le long d'un tracé, avec styles de caractère et de
  paragraphe.
- Plusieurs plans de travail par dessin, calques, groupes, symboles, dégradés de formes en direct,
  repères commentés et règles.
- Annulation sans limite.

## Vos fichiers Kazo OS

- Double-cliquez sur un fichier `.svg`, `.svgz`, `.ai`, `.ait`, `.eps`, `.epsf`, `.ps`, `.dxf`,
  `.emf`, `.wmf`, `.vectorcraft` ou `.vctemplate` dans l'Explorateur de fichiers : il s'ouvre dans
  Kazo Vector. Les fichiers `.pdf`
  et Affinity `.afdesign` le proposent dans « Ouvrir avec ».
- Plusieurs dessins par fenêtre, chacun dans son onglet. Fichier › Ouvrir passe par le dialogue
  d'ouverture de Kazo OS et peut ouvrir plusieurs fichiers d'un coup. Un fichier ouvert depuis
  l'Explorateur a sa propre fenêtre.
- Enregistrer réécrit le fichier ouvert, au même format. Enregistrer sous, et le premier
  enregistrement d'un nouveau dessin, passent par le dialogue d'enregistrement de Kazo OS.
- Enregistrer une copie et Exporter (SVG, PDF, PNG, JPEG, WebP, export pour écrans) passent par le
  dialogue « Enregistrer une copie » de Kazo OS : le dessin reste lié à son fichier. Rien ne part
  dans les téléchargements du navigateur.
- Fermer la fenêtre demande avant de perdre des modifications, et peut d'abord enregistrer chaque
  dessin modifié.
- Kazo Vector suit le thème clair ou sombre de Kazo OS et sa langue.

## Limites connues

- **VectorCraft est en alpha.** La plupart des dessins s'ouvrent et s'enregistrent bien, mais
  gardez une copie des plus importants.
- Kazo OS ouvre les fichiers jusqu'à 128 Mio. Les dessins très grands ou très complexes peuvent être
  lents à ouvrir et à afficher, surtout sur un ordinateur sans carte graphique.
- Pas de récupération après un arrêt brutal pour l'instant : un dessin non enregistré est perdu si
  le navigateur ou l'onglet se ferme. Enregistrez souvent.
- Les réglages de VectorCraft (options des outils, panneaux, préférences) ne sont pas gardés d'une
  fenêtre à l'autre.
- Le premier enregistrement d'un nouveau dessin montre deux dialogues : les options
  d'enregistrement de VectorCraft (nom et format), puis le dialogue d'enregistrement de Kazo OS
  (dossier).
- Imprimer passe par le dialogue d'impression du navigateur.
- Kazo OS garde Alt+Maj+Gauche/Droite et Ctrl+Alt+Maj+Gauche/Droite : dans le texte vertical,
  décalez la ligne de base depuis le panneau Caractère. Alt+Espace ouvre le menu de la fenêtre :
  lâchez Alt avant de déplacer la vue avec Espace.
- La fenêtre reste sur l'écran où elle s'est ouverte : la passer sur un autre écran rechargerait
  VectorCraft et perdrait les dessins ouverts.
- VectorCraft dessine avec WebGPU ou WebGL 2 : un navigateur ou un ordinateur sans l'un ni l'autre
  montre un écran d'erreur au lieu du dessin.
- Le téléchargement fait environ 30 Mo (environ 72 Mo une fois installé), et chaque fenêtre charge
  sa propre copie de VectorCraft.
- Demande Kazo OS 3.3.4 ou plus récent.

## Licence

Kazo Vector est un logiciel libre sous licence MIT. VectorCraft est sous licence MIT ou Apache-2.0 ;
ses avis, les licences du matériel qu'il contient, et celles des polices (SIL Open Font License
1.1) sont livrés avec l'app (voir `THIRD-PARTY-NOTICES-fr.md` dans le dépôt). Le nom et les logos
ArtCraft sont des marques de l'équipe ArtCraft et ne sont pas utilisés par Kazo Vector.
