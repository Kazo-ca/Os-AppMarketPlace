# Kazo IA

Une discussion avec des modèles d'IA pour Kazo OS, écrite en C# et Blazor, sur ordinateur et sur
téléphone. Apportez vos propres clés d'API, ou faites tourner un modèle directement dans votre
navigateur : rien à installer, aucun compte nécessaire.

## Vos modèles, vos clés

- **OpenRouter, OpenAI, Ollama, LM Studio**, et tout service qui parle l'API de chat d'OpenAI :
  ajoutez son adresse, collez votre clé, listez ses modèles et choisissez-en un.
- Les clés restent dans le **trousseau de Kazo OS** et ne sont envoyées qu'à leur propre service,
  directement depuis le navigateur. Elles ne sont jamais écrites dans un fichier ni exportées.
- Les réponses s'affichent à mesure qu'elles s'écrivent, en Markdown, avec blocs de code et
  tableaux.

## Un modèle local, sur votre carte graphique

- Pas de clé ? Kazo IA fait tourner un petit modèle ouvert **dans le navigateur avec WebGPU**
  (WebLLM) : Qwen, Llama, Phi, Gemma et d'autres. Le modèle est téléchargé une fois et gardé dans le
  cache du navigateur.
- Ce que vous écrivez au modèle local ne quitte jamais votre ordinateur.
- Il se charge tout seul au démarrage s'il est déjà téléchargé, et prend le relais quand le service
  choisi n'a pas de clé.

## Course et AutoTune

- Le **mode course** interroge plusieurs modèles à la fois, note leurs réponses à mesure qu'elles
  arrivent, montre le meneur en direct et garde la meilleure, avec le classement complet.
- **AutoTune** lit la conversation (code, écriture, analyse, bavardage...) et règle la température et
  les autres paramètres d'échantillonnage pour chaque réponse. Notez les réponses avec 👍 et 👎 et il
  apprend vos goûts ; ou choisissez une stratégie fixe, ou réglez tout à la main.

## Personas et modules

- Les personas (assistant, développeur, écrivain, professeur, ou les vôtres) donnent le prompt
  système d'une conversation.
- Les **modules** changent la requête avant son départ ou la réponse avant son affichage : quelques-uns
  sont intégrés, d'autres s'importent comme de simples fichiers JSON, et les développeurs peuvent
  compiler leurs propres modules C# dans l'app.
- Réglages, personas et modules s'exportent et s'importent en un seul fichier JSON.

## Approbation de l'administrateur

Kazo IA demande l'accès au **réseau réel** (`network.real.access`) pour joindre les services d'IA
et télécharger le modèle local. Kazo OS demande à un administrateur de l'approuver au premier
lancement.
