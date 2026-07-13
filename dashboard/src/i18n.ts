export type DashboardLanguage = 'en' | 'es' | 'fr' | 'it' | 'pt' | 'zh' | 'yue' | 'ja-kanji' | 'ja' | 'fa';

export interface DashboardLanguageOption {
  Code: DashboardLanguage;
  Label: string;
  NativeLabel: string;
  Direction: 'ltr' | 'rtl';
}

const languageKey = 'tablix_language';

export const dashboardLanguages: DashboardLanguageOption[] = [
  { Code: 'en', Label: 'English', NativeLabel: 'English', Direction: 'ltr' },
  { Code: 'es', Label: 'Spanish', NativeLabel: 'Español', Direction: 'ltr' },
  { Code: 'fr', Label: 'French', NativeLabel: 'Français', Direction: 'ltr' },
  { Code: 'it', Label: 'Italian', NativeLabel: 'Italiano', Direction: 'ltr' },
  { Code: 'pt', Label: 'Portuguese', NativeLabel: 'Português', Direction: 'ltr' },
  { Code: 'zh', Label: 'Mandarin', NativeLabel: '普通话', Direction: 'ltr' },
  { Code: 'yue', Label: 'Cantonese', NativeLabel: '廣東話', Direction: 'ltr' },
  { Code: 'ja-kanji', Label: 'Kanji', NativeLabel: '日本語（漢字）', Direction: 'ltr' },
  { Code: 'ja', Label: 'Japanese', NativeLabel: '日本語', Direction: 'ltr' },
  { Code: 'fa', Label: 'Farsi', NativeLabel: 'فارسی', Direction: 'rtl' }
];

const phrases: Record<DashboardLanguage, Record<string, string>> = {
  en: {
    'Tablix': 'Tablix',
    'Databases': 'Databases',
    'Query': 'Query',
    'Chat': 'Chat',
    'Models': 'Models',
    'Settings': 'Settings',
    'Server URL': 'Server URL',
    'API Key': 'API Key',
    'Sign In': 'Sign In',
    'Add Database': 'Add Database',
    'Edit Database': 'Edit Database',
    'Save Database': 'Save Database',
    'Create Database': 'Create Database',
    'Cancel': 'Cancel',
    'Confirm': 'Confirm',
    'Delete': 'Delete',
    'Edit': 'Edit',
    'View': 'View',
    'Close': 'Close',
    'Copy': 'Copy',
    'Refresh': 'Refresh',
    'Download CSV': 'Download CSV',
    'Database': 'Database',
    'Provider': 'Provider',
    'Streaming': 'Streaming',
    'Ask about the selected database.': 'Ask about the selected database.',
    'Responses can include markdown, SQL, tables, and lists.': 'Responses can include markdown, SQL, tables, and lists.',
    'Enter to send, Shift+Enter for newline': 'Enter to send, Shift+Enter for newline',
    'Send': 'Send',
    'Sending...': 'Sending...',
    'API key configured': 'API key configured',
    'No API key': 'No API key',
    'Native tools': 'Native tools',
    'Server fallback': 'Server fallback',
    'The model did not request a tool call.': 'The model did not request a tool call.',
    'Tool execution failed; the assistant returned a plain model response.': 'Tool execution failed; the assistant returned a plain model response.',
    'Tablix could not plan a database query for this request.': 'Tablix could not plan a database query for this request.',
    'Tablix ran a database query to answer this message.': 'Tablix ran a database query to answer this message.',
    'This provider is not configured for native tool calls. Tablix can use server-side fallback execution for database data requests.': 'This provider is not configured for native tool calls. Tablix can use server-side fallback execution for database data requests.',
    'Failed': 'Failed',
    'Complete': 'Complete',
    'Running': 'Running',
    'Message telemetry': 'Message telemetry',
    'Time to first token': 'Time to first token',
    'Total streaming time': 'Total streaming time',
    'Input tokens': 'Input tokens',
    'Output tokens': 'Output tokens',
    'Total tokens': 'Total tokens',
    'Configured database connections': 'Configured database connections',
    'ID': 'ID',
    'Name': 'Name',
    'Type': 'Type',
    'Schema': 'Schema',
    'Actions': 'Actions',
    'Build Context': 'Build Context',
    'Build and Save': 'Build and Save',
    'Building...': 'Building...',
    'Prompt': 'Prompt',
    'Context': 'Context',
    'Table Context': 'Table Context',
    'No context saved.': 'No context saved.',
    'Edit Context': 'Edit Context',
    'Save Context': 'Save Context',
    'Saving...': 'Saving...',
    'Context saved.': 'Context saved.',
    'Context built and saved.': 'Context built and saved.',
    'Tables': 'Tables',
    'Column': 'Column',
    'Nullable': 'Nullable',
    'Default': 'Default',
    'Status': 'Status',
    'Crawled': 'Crawled',
    'Degraded': 'Degraded',
    'Last Crawl': 'Last Crawl',
    'Error': 'Error',
    'Crawl Status': 'Crawl Status',
    'Preparing crawl.': 'Preparing crawl.',
    'Elapsed': 'Elapsed',
    'Current table': 'Current table',
    'Relationships': 'Relationships',
    'SQL Query': 'SQL Query',
    'Execute': 'Execute',
    'Executing...': 'Executing...',
    'No rows returned.': 'No rows returned.',
    'Could not connect to server.': 'Could not connect to server.',
    'Query failed.': 'Query failed.',
    'Loading settings...': 'Loading settings...',
    'Save Settings': 'Save Settings',
    'Persistence': 'Persistence',
    'REST and MCP': 'REST and MCP',
    'API Keys': 'API Keys',
    'Logging': 'Logging',
    'Chat enabled': 'Chat enabled',
    'Default streaming': 'Default streaming',
    'No default provider': 'No default provider',
    'System Prompt': 'System Prompt',
    'Prompt Processing': 'Prompt Processing',
    'Enabled': 'Enabled',
    'Prefer native tools': 'Prefer native tools',
    'Execute data requests': 'Execute data requests',
    'Honor SQL-only requests': 'Honor SQL-only requests',
    'Retry after schema refresh': 'Retry after schema refresh',
    'Chat Tools': 'Chat Tools',
    'Tools enabled': 'Tools enabled',
    'Read-only queries': 'Read-only queries',
    'Context updates': 'Context updates',
    'Restart': 'Restart',
    'Model Providers': 'Model Providers',
    'Add Model': 'Add Model',
    'Edit Model': 'Edit Model',
    'Test Provider': 'Test Provider',
    'Testing Provider': 'Testing Provider',
    'Provider ID': 'Provider ID',
    'Endpoint': 'Endpoint',
    'Model': 'Model',
    'Max Concurrent Requests': 'Max Concurrent Requests',
    'Supports native tools': 'Supports native tools',
    'Use native tools': 'Use native tools',
    'Default Streaming': 'Default Streaming',
    'Strict JSON': 'Strict JSON',
    'Temperature': 'Temperature',
    'Top P': 'Top P',
    'Max Tokens': 'Max Tokens',
    'Request Timeout': 'Request Timeout',
    'Clear API key': 'Clear API key',
    'Save Model': 'Save Model',
    'Set Up Tablix': 'Set Up Tablix',
    'Model Provider': 'Model Provider',
    'Database Context': 'Database Context',
    'Crawl Database': 'Crawl Database',
    'Ready for Chat': 'Ready for Chat',
    'Save and Continue': 'Save and Continue',
    'Test Database': 'Test Database',
    'Start Crawl': 'Start Crawl',
    'Crawling...': 'Crawling...',
    'Build Database Context': 'Build Database Context',
    'Save Edited Contexts': 'Save Edited Contexts',
    'Build Table Contexts': 'Build Table Contexts',
    'Go to Chat When Ready': 'Go to Chat When Ready',
    'Skip setup': 'Skip setup',
    'Exit setup wizard': 'Exit setup wizard',
    'Allowed Queries': 'Allowed Queries',
    'Generation Instructions': 'Generation Instructions',
    'Table': 'Table',
    'Generate durable context from the latest crawl with the selected provider.': 'Generate durable context from the latest crawl with the selected provider.',
    'This operation may take some time, please be patient.': 'This operation may take some time, please be patient.',
    'Your provider, database, crawl metadata, and context are stored. Open Chat when you are ready to ask questions.': 'Your provider, database, crawl metadata, and context are stored. Open Chat when you are ready to ask questions.',
    'Tablix is validating this provider with the current settings.': 'Tablix is validating this provider with the current settings.',
    'Hostname': 'Hostname',
    'Port': 'Port',
    'User': 'User',
    'Password': 'Password',
    'Filename': 'Filename',
    'Database Name': 'Database Name',
    'Allowed Queries (comma-separated)': 'Allowed Queries (comma-separated)',
    'Working...': 'Working...',
    'Delete Database': 'Delete Database',
    'Delete Model': 'Delete Model'
  },
  es: {
    'Databases': 'Bases de datos', 'Query': 'Consulta', 'Chat': 'Chat', 'Models': 'Modelos', 'Settings': 'Configuración', 'Server URL': 'URL del servidor', 'API Key': 'Clave API', 'Sign In': 'Iniciar sesión', 'Add Database': 'Agregar base de datos', 'Edit Database': 'Editar base de datos', 'Save Database': 'Guardar base de datos', 'Create Database': 'Crear base de datos', 'Cancel': 'Cancelar', 'Confirm': 'Confirmar', 'Delete': 'Eliminar', 'Edit': 'Editar', 'View': 'Ver', 'Close': 'Cerrar', 'Copy': 'Copiar', 'Refresh': 'Actualizar', 'Download CSV': 'Descargar CSV', 'Database': 'Base de datos', 'Provider': 'Proveedor', 'Streaming': 'Transmisión', 'Ask about the selected database.': 'Pregunta sobre la base de datos seleccionada.', 'Responses can include markdown, SQL, tables, and lists.': 'Las respuestas pueden incluir Markdown, SQL, tablas y listas.', 'Enter to send, Shift+Enter for newline': 'Enter para enviar, Shift+Enter para nueva línea', 'Send': 'Enviar', 'Sending...': 'Enviando...', 'API key configured': 'Clave API configurada', 'No API key': 'Sin clave API', 'Native tools': 'Herramientas nativas', 'Server fallback': 'Alternativa del servidor', 'The model did not request a tool call.': 'El modelo no solicitó una llamada a herramienta.', 'Tool execution failed; the assistant returned a plain model response.': 'La ejecución de la herramienta falló; el asistente devolvió una respuesta simple del modelo.', 'Tablix could not plan a database query for this request.': 'Tablix no pudo planificar una consulta de base de datos para esta solicitud.', 'Tablix ran a database query to answer this message.': 'Tablix ejecutó una consulta de base de datos para responder este mensaje.', 'This provider is not configured for native tool calls. Tablix can use server-side fallback execution for database data requests.': 'Este proveedor no está configurado para llamadas nativas a herramientas. Tablix puede usar ejecución alternativa en el servidor para solicitudes de datos.', 'Failed': 'Falló', 'Complete': 'Completo', 'Running': 'En ejecución', 'Message telemetry': 'Telemetría del mensaje', 'Time to first token': 'Tiempo hasta el primer token', 'Total streaming time': 'Tiempo total de transmisión', 'Input tokens': 'Tokens de entrada', 'Output tokens': 'Tokens de salida', 'Total tokens': 'Tokens totales', 'Configured database connections': 'Conexiones de base de datos configuradas', 'ID': 'ID', 'Name': 'Nombre', 'Type': 'Tipo', 'Schema': 'Esquema', 'Actions': 'Acciones', 'Build Context': 'Crear contexto', 'Build and Save': 'Crear y guardar', 'Building...': 'Creando...', 'Prompt': 'Instrucciones', 'Context': 'Contexto', 'Table Context': 'Contexto de tabla', 'No context saved.': 'No hay contexto guardado.', 'Edit Context': 'Editar contexto', 'Save Context': 'Guardar contexto', 'Saving...': 'Guardando...', 'Context saved.': 'Contexto guardado.', 'Context built and saved.': 'Contexto creado y guardado.', 'Tables': 'Tablas', 'Column': 'Columna', 'Nullable': 'Nulo permitido', 'Default': 'Predeterminado', 'Status': 'Estado', 'Crawled': 'Rastreado', 'Degraded': 'Degradado', 'Last Crawl': 'Último rastreo', 'Error': 'Error', 'Crawl Status': 'Estado del rastreo', 'Preparing crawl.': 'Preparando rastreo.', 'Elapsed': 'Transcurrido', 'Current table': 'Tabla actual', 'Relationships': 'Relaciones', 'SQL Query': 'Consulta SQL', 'Execute': 'Ejecutar', 'Executing...': 'Ejecutando...', 'No rows returned.': 'No se devolvieron filas.', 'Could not connect to server.': 'No se pudo conectar al servidor.', 'Query failed.': 'La consulta falló.', 'Loading settings...': 'Cargando configuración...', 'Save Settings': 'Guardar configuración', 'Persistence': 'Persistencia', 'REST and MCP': 'REST y MCP', 'API Keys': 'Claves API', 'Logging': 'Registro', 'Chat enabled': 'Chat habilitado', 'Default streaming': 'Transmisión predeterminada', 'No default provider': 'Sin proveedor predeterminado', 'System Prompt': 'Prompt del sistema', 'Prompt Processing': 'Procesamiento de prompts', 'Enabled': 'Habilitado', 'Prefer native tools': 'Preferir herramientas nativas', 'Execute data requests': 'Ejecutar solicitudes de datos', 'Honor SQL-only requests': 'Respetar solicitudes solo SQL', 'Retry after schema refresh': 'Reintentar tras actualizar esquema', 'Chat Tools': 'Herramientas de chat', 'Tools enabled': 'Herramientas habilitadas', 'Read-only queries': 'Consultas de solo lectura', 'Context updates': 'Actualizaciones de contexto', 'Restart': 'Reiniciar', 'Model Providers': 'Proveedores de modelos', 'Add Model': 'Agregar modelo', 'Edit Model': 'Editar modelo', 'Test Provider': 'Probar proveedor', 'Testing Provider': 'Probando proveedor', 'Provider ID': 'ID del proveedor', 'Endpoint': 'Endpoint', 'Model': 'Modelo', 'Max Concurrent Requests': 'Solicitudes concurrentes máximas', 'Supports native tools': 'Admite herramientas nativas', 'Use native tools': 'Usar herramientas nativas', 'Default Streaming': 'Transmisión predeterminada', 'Strict JSON': 'JSON estricto', 'Temperature': 'Temperatura', 'Top P': 'Top P', 'Max Tokens': 'Tokens máximos', 'Request Timeout': 'Tiempo de espera de solicitud', 'Clear API key': 'Borrar clave API', 'Save Model': 'Guardar modelo', 'Set Up Tablix': 'Configurar Tablix', 'Model Provider': 'Proveedor de modelo', 'Database Context': 'Contexto de base de datos', 'Crawl Database': 'Rastrear base de datos', 'Ready for Chat': 'Listo para chat', 'Save and Continue': 'Guardar y continuar', 'Test Database': 'Probar base de datos', 'Start Crawl': 'Iniciar rastreo', 'Crawling...': 'Rastreando...', 'Build Database Context': 'Crear contexto de base de datos', 'Save Edited Contexts': 'Guardar contextos editados', 'Build Table Contexts': 'Crear contextos de tabla', 'Go to Chat When Ready': 'Ir al chat cuando esté listo', 'Skip setup': 'Omitir configuración', 'Exit setup wizard': 'Salir del asistente', 'Allowed Queries': 'Consultas permitidas', 'Generation Instructions': 'Instrucciones de generación', 'Table': 'Tabla', 'Generate durable context from the latest crawl with the selected provider.': 'Genera contexto durable desde el último rastreo con el proveedor seleccionado.', 'This operation may take some time, please be patient.': 'Esta operación puede tardar; ten paciencia.', 'Your provider, database, crawl metadata, and context are stored. Open Chat when you are ready to ask questions.': 'Tu proveedor, base de datos, metadatos de rastreo y contexto están guardados. Abre Chat cuando quieras preguntar.', 'Tablix is validating this provider with the current settings.': 'Tablix está validando este proveedor con la configuración actual.', 'Hostname': 'Nombre de host', 'Port': 'Puerto', 'User': 'Usuario', 'Password': 'Contraseña', 'Filename': 'Archivo', 'Database Name': 'Nombre de base de datos', 'Allowed Queries (comma-separated)': 'Consultas permitidas (separadas por comas)', 'Working...': 'Trabajando...', 'Delete Database': 'Eliminar base de datos', 'Delete Model': 'Eliminar modelo'
  },
  fr: {
    'Databases': 'Bases de données', 'Query': 'Requête', 'Chat': 'Chat', 'Models': 'Modèles', 'Settings': 'Paramètres', 'Server URL': 'URL du serveur', 'API Key': 'Clé API', 'Sign In': 'Se connecter', 'Add Database': 'Ajouter une base', 'Edit Database': 'Modifier la base', 'Save Database': 'Enregistrer la base', 'Create Database': 'Créer la base', 'Cancel': 'Annuler', 'Confirm': 'Confirmer', 'Delete': 'Supprimer', 'Edit': 'Modifier', 'View': 'Afficher', 'Close': 'Fermer', 'Copy': 'Copier', 'Refresh': 'Actualiser', 'Download CSV': 'Télécharger CSV', 'Database': 'Base de données', 'Provider': 'Fournisseur', 'Streaming': 'Streaming', 'Ask about the selected database.': 'Posez une question sur la base sélectionnée.', 'Responses can include markdown, SQL, tables, and lists.': 'Les réponses peuvent inclure Markdown, SQL, tableaux et listes.', 'Enter to send, Shift+Enter for newline': 'Entrée pour envoyer, Maj+Entrée pour une nouvelle ligne', 'Send': 'Envoyer', 'Sending...': 'Envoi...', 'API key configured': 'Clé API configurée', 'No API key': 'Aucune clé API', 'Native tools': 'Outils natifs', 'Server fallback': 'Repli serveur', 'The model did not request a tool call.': "Le modèle n'a pas demandé d'appel d'outil.", 'Tool execution failed; the assistant returned a plain model response.': "L'exécution de l'outil a échoué; l'assistant a renvoyé une réponse simple du modèle.", 'Tablix could not plan a database query for this request.': "Tablix n'a pas pu planifier une requête de base de données pour cette demande.", 'Tablix ran a database query to answer this message.': 'Tablix a exécuté une requête de base de données pour répondre à ce message.', 'This provider is not configured for native tool calls. Tablix can use server-side fallback execution for database data requests.': "Ce fournisseur n'est pas configuré pour les appels d'outils natifs. Tablix peut utiliser un repli côté serveur pour les demandes de données.", 'Failed': 'Échec', 'Complete': 'Terminé', 'Running': 'En cours', 'Message telemetry': 'Télémétrie du message', 'Time to first token': 'Temps au premier token', 'Total streaming time': 'Temps total de streaming', 'Input tokens': "Tokens d'entrée", 'Output tokens': 'Tokens de sortie', 'Total tokens': 'Tokens totaux', 'Configured database connections': 'Connexions de base configurées', 'ID': 'ID', 'Name': 'Nom', 'Type': 'Type', 'Schema': 'Schéma', 'Actions': 'Actions', 'Build Context': 'Générer le contexte', 'Build and Save': 'Générer et enregistrer', 'Building...': 'Génération...', 'Prompt': 'Instructions', 'Context': 'Contexte', 'Table Context': 'Contexte de table', 'No context saved.': 'Aucun contexte enregistré.', 'Edit Context': 'Modifier le contexte', 'Save Context': 'Enregistrer le contexte', 'Saving...': 'Enregistrement...', 'Context saved.': 'Contexte enregistré.', 'Context built and saved.': 'Contexte généré et enregistré.', 'Tables': 'Tables', 'Column': 'Colonne', 'Nullable': 'Nullable', 'Default': 'Défaut', 'Status': 'État', 'Crawled': 'Exploré', 'Degraded': 'Dégradé', 'Last Crawl': 'Dernière exploration', 'Error': 'Erreur', 'Crawl Status': "État de l'exploration", 'Preparing crawl.': "Préparation de l'exploration.", 'Elapsed': 'Écoulé', 'Current table': 'Table actuelle', 'Relationships': 'Relations', 'SQL Query': 'Requête SQL', 'Execute': 'Exécuter', 'Executing...': 'Exécution...', 'No rows returned.': 'Aucune ligne retournée.', 'Could not connect to server.': 'Impossible de se connecter au serveur.', 'Query failed.': 'La requête a échoué.', 'Loading settings...': 'Chargement des paramètres...', 'Save Settings': 'Enregistrer les paramètres', 'Persistence': 'Persistance', 'REST and MCP': 'REST et MCP', 'API Keys': 'Clés API', 'Logging': 'Journalisation', 'Chat enabled': 'Chat activé', 'Default streaming': 'Streaming par défaut', 'No default provider': 'Aucun fournisseur par défaut', 'System Prompt': 'Prompt système', 'Prompt Processing': 'Traitement des prompts', 'Enabled': 'Activé', 'Prefer native tools': 'Préférer les outils natifs', 'Execute data requests': 'Exécuter les demandes de données', 'Honor SQL-only requests': 'Respecter les demandes SQL seules', 'Retry after schema refresh': 'Réessayer après actualisation du schéma', 'Chat Tools': 'Outils de chat', 'Tools enabled': 'Outils activés', 'Read-only queries': 'Requêtes en lecture seule', 'Context updates': 'Mises à jour du contexte', 'Restart': 'Redémarrer', 'Model Providers': 'Fournisseurs de modèles', 'Add Model': 'Ajouter un modèle', 'Edit Model': 'Modifier le modèle', 'Test Provider': 'Tester le fournisseur', 'Testing Provider': 'Test du fournisseur', 'Provider ID': 'ID du fournisseur', 'Endpoint': 'Endpoint', 'Model': 'Modèle', 'Max Concurrent Requests': 'Requêtes concurrentes max', 'Supports native tools': 'Prend en charge les outils natifs', 'Use native tools': 'Utiliser les outils natifs', 'Default Streaming': 'Streaming par défaut', 'Strict JSON': 'JSON strict', 'Temperature': 'Température', 'Top P': 'Top P', 'Max Tokens': 'Tokens max', 'Request Timeout': 'Délai de requête', 'Clear API key': 'Effacer la clé API', 'Save Model': 'Enregistrer le modèle', 'Set Up Tablix': 'Configurer Tablix', 'Model Provider': 'Fournisseur de modèle', 'Database Context': 'Contexte de base', 'Crawl Database': 'Explorer la base', 'Ready for Chat': 'Prêt pour le chat', 'Save and Continue': 'Enregistrer et continuer', 'Test Database': 'Tester la base', 'Start Crawl': "Démarrer l'exploration", 'Crawling...': 'Exploration...', 'Build Database Context': 'Générer le contexte de base', 'Save Edited Contexts': 'Enregistrer les contextes modifiés', 'Build Table Contexts': 'Générer les contextes de table', 'Go to Chat When Ready': 'Aller au chat quand prêt', 'Skip setup': 'Ignorer la configuration', 'Exit setup wizard': "Quitter l'assistant", 'Allowed Queries': 'Requêtes autorisées', 'Generation Instructions': 'Instructions de génération', 'Table': 'Table', 'Generate durable context from the latest crawl with the selected provider.': 'Générez un contexte durable à partir de la dernière exploration avec le fournisseur sélectionné.', 'This operation may take some time, please be patient.': 'Cette opération peut prendre du temps, veuillez patienter.', 'Your provider, database, crawl metadata, and context are stored. Open Chat when you are ready to ask questions.': 'Votre fournisseur, base, métadonnées et contexte sont enregistrés. Ouvrez Chat quand vous êtes prêt.', 'Tablix is validating this provider with the current settings.': 'Tablix valide ce fournisseur avec les paramètres actuels.', 'Hostname': "Nom d'hôte", 'Port': 'Port', 'User': 'Utilisateur', 'Password': 'Mot de passe', 'Filename': 'Fichier', 'Database Name': 'Nom de base', 'Allowed Queries (comma-separated)': 'Requêtes autorisées (séparées par des virgules)', 'Working...': 'Traitement...', 'Delete Database': 'Supprimer la base', 'Delete Model': 'Supprimer le modèle'
  },
  it: {
    'Databases': 'Database', 'Query': 'Query', 'Chat': 'Chat', 'Models': 'Modelli', 'Settings': 'Impostazioni', 'Server URL': 'URL server', 'API Key': 'Chiave API', 'Sign In': 'Accedi', 'Add Database': 'Aggiungi database', 'Edit Database': 'Modifica database', 'Save Database': 'Salva database', 'Create Database': 'Crea database', 'Cancel': 'Annulla', 'Confirm': 'Conferma', 'Delete': 'Elimina', 'Edit': 'Modifica', 'View': 'Visualizza', 'Close': 'Chiudi', 'Copy': 'Copia', 'Refresh': 'Aggiorna', 'Download CSV': 'Scarica CSV', 'Database': 'Database', 'Provider': 'Provider', 'Streaming': 'Streaming', 'Ask about the selected database.': 'Fai una domanda sul database selezionato.', 'Responses can include markdown, SQL, tables, and lists.': 'Le risposte possono includere Markdown, SQL, tabelle ed elenchi.', 'Enter to send, Shift+Enter for newline': 'Invio per inviare, Maiusc+Invio per nuova riga', 'Send': 'Invia', 'Sending...': 'Invio...', 'API key configured': 'Chiave API configurata', 'No API key': 'Nessuna chiave API', 'Native tools': 'Strumenti nativi', 'Server fallback': 'Fallback server', 'The model did not request a tool call.': 'Il modello non ha richiesto una chiamata a strumento.', 'Tool execution failed; the assistant returned a plain model response.': "L'esecuzione dello strumento è fallita; l'assistente ha restituito una risposta semplice del modello.", 'Tablix could not plan a database query for this request.': 'Tablix non ha potuto pianificare una query di database per questa richiesta.', 'Tablix ran a database query to answer this message.': 'Tablix ha eseguito una query di database per rispondere a questo messaggio.', 'This provider is not configured for native tool calls. Tablix can use server-side fallback execution for database data requests.': 'Questo provider non è configurato per chiamate native agli strumenti. Tablix può usare il fallback lato server per richieste dati.', 'Failed': 'Non riuscito', 'Complete': 'Completo', 'Running': 'In esecuzione', 'Message telemetry': 'Telemetria messaggio', 'Time to first token': 'Tempo al primo token', 'Total streaming time': 'Tempo totale streaming', 'Input tokens': 'Token input', 'Output tokens': 'Token output', 'Total tokens': 'Token totali', 'Configured database connections': 'Connessioni database configurate', 'ID': 'ID', 'Name': 'Nome', 'Type': 'Tipo', 'Schema': 'Schema', 'Actions': 'Azioni', 'Build Context': 'Genera contesto', 'Build and Save': 'Genera e salva', 'Building...': 'Generazione...', 'Prompt': 'Prompt', 'Context': 'Contesto', 'Table Context': 'Contesto tabella', 'No context saved.': 'Nessun contesto salvato.', 'Edit Context': 'Modifica contesto', 'Save Context': 'Salva contesto', 'Saving...': 'Salvataggio...', 'Context saved.': 'Contesto salvato.', 'Context built and saved.': 'Contesto generato e salvato.', 'Tables': 'Tabelle', 'Column': 'Colonna', 'Nullable': 'Nullable', 'Default': 'Predefinito', 'Status': 'Stato', 'Crawled': 'Scansionato', 'Degraded': 'Degradato', 'Last Crawl': 'Ultima scansione', 'Error': 'Errore', 'Crawl Status': 'Stato scansione', 'Preparing crawl.': 'Preparazione scansione.', 'Elapsed': 'Trascorso', 'Current table': 'Tabella corrente', 'Relationships': 'Relazioni', 'SQL Query': 'Query SQL', 'Execute': 'Esegui', 'Executing...': 'Esecuzione...', 'No rows returned.': 'Nessuna riga restituita.', 'Could not connect to server.': 'Impossibile connettersi al server.', 'Query failed.': 'Query non riuscita.', 'Loading settings...': 'Caricamento impostazioni...', 'Save Settings': 'Salva impostazioni', 'Persistence': 'Persistenza', 'REST and MCP': 'REST e MCP', 'API Keys': 'Chiavi API', 'Logging': 'Log', 'Chat enabled': 'Chat abilitata', 'Default streaming': 'Streaming predefinito', 'No default provider': 'Nessun provider predefinito', 'System Prompt': 'Prompt di sistema', 'Prompt Processing': 'Elaborazione prompt', 'Enabled': 'Abilitato', 'Prefer native tools': 'Preferisci strumenti nativi', 'Execute data requests': 'Esegui richieste dati', 'Honor SQL-only requests': 'Rispetta richieste solo SQL', 'Retry after schema refresh': 'Riprova dopo aggiornamento schema', 'Chat Tools': 'Strumenti chat', 'Tools enabled': 'Strumenti abilitati', 'Read-only queries': 'Query sola lettura', 'Context updates': 'Aggiornamenti contesto', 'Restart': 'Riavvio', 'Model Providers': 'Provider modelli', 'Add Model': 'Aggiungi modello', 'Edit Model': 'Modifica modello', 'Test Provider': 'Test provider', 'Testing Provider': 'Test provider', 'Provider ID': 'ID provider', 'Endpoint': 'Endpoint', 'Model': 'Modello', 'Max Concurrent Requests': 'Richieste concorrenti max', 'Supports native tools': 'Supporta strumenti nativi', 'Use native tools': 'Usa strumenti nativi', 'Default Streaming': 'Streaming predefinito', 'Strict JSON': 'JSON rigoroso', 'Temperature': 'Temperatura', 'Top P': 'Top P', 'Max Tokens': 'Token max', 'Request Timeout': 'Timeout richiesta', 'Clear API key': 'Cancella chiave API', 'Save Model': 'Salva modello', 'Set Up Tablix': 'Configura Tablix', 'Model Provider': 'Provider modello', 'Database Context': 'Contesto database', 'Crawl Database': 'Scansiona database', 'Ready for Chat': 'Pronto per chat', 'Save and Continue': 'Salva e continua', 'Test Database': 'Test database', 'Start Crawl': 'Avvia scansione', 'Crawling...': 'Scansione...', 'Build Database Context': 'Genera contesto database', 'Save Edited Contexts': 'Salva contesti modificati', 'Build Table Contexts': 'Genera contesti tabella', 'Go to Chat When Ready': 'Vai alla chat quando pronto', 'Skip setup': 'Salta configurazione', 'Exit setup wizard': 'Esci dalla procedura', 'Allowed Queries': 'Query consentite', 'Generation Instructions': 'Istruzioni di generazione', 'Table': 'Tabella', 'Generate durable context from the latest crawl with the selected provider.': "Genera contesto duraturo dall'ultima scansione con il provider selezionato.", 'This operation may take some time, please be patient.': 'Questa operazione può richiedere tempo, attendere.', 'Your provider, database, crawl metadata, and context are stored. Open Chat when you are ready to ask questions.': 'Provider, database, metadati e contesto sono salvati. Apri Chat quando vuoi fare domande.', 'Tablix is validating this provider with the current settings.': 'Tablix sta validando questo provider con le impostazioni attuali.', 'Hostname': 'Hostname', 'Port': 'Porta', 'User': 'Utente', 'Password': 'Password', 'Filename': 'File', 'Database Name': 'Nome database', 'Allowed Queries (comma-separated)': 'Query consentite (separate da virgole)', 'Working...': 'Elaborazione...', 'Delete Database': 'Elimina database', 'Delete Model': 'Elimina modello'
  },
  pt: {
    'Databases': 'Bancos de dados', 'Query': 'Consulta', 'Chat': 'Chat', 'Models': 'Modelos', 'Settings': 'Configurações', 'Server URL': 'URL do servidor', 'API Key': 'Chave API', 'Sign In': 'Entrar', 'Add Database': 'Adicionar banco', 'Edit Database': 'Editar banco', 'Save Database': 'Salvar banco', 'Create Database': 'Criar banco', 'Cancel': 'Cancelar', 'Confirm': 'Confirmar', 'Delete': 'Excluir', 'Edit': 'Editar', 'View': 'Ver', 'Close': 'Fechar', 'Copy': 'Copiar', 'Refresh': 'Atualizar', 'Download CSV': 'Baixar CSV', 'Database': 'Banco de dados', 'Provider': 'Provedor', 'Streaming': 'Streaming', 'Ask about the selected database.': 'Pergunte sobre o banco selecionado.', 'Responses can include markdown, SQL, tables, and lists.': 'As respostas podem incluir Markdown, SQL, tabelas e listas.', 'Enter to send, Shift+Enter for newline': 'Enter para enviar, Shift+Enter para nova linha', 'Send': 'Enviar', 'Sending...': 'Enviando...', 'API key configured': 'Chave API configurada', 'No API key': 'Sem chave API', 'Native tools': 'Ferramentas nativas', 'Server fallback': 'Fallback do servidor', 'The model did not request a tool call.': 'O modelo não solicitou uma chamada de ferramenta.', 'Tool execution failed; the assistant returned a plain model response.': 'A execução da ferramenta falhou; o assistente retornou uma resposta simples do modelo.', 'Tablix could not plan a database query for this request.': 'Tablix não conseguiu planejar uma consulta de banco de dados para esta solicitação.', 'Tablix ran a database query to answer this message.': 'Tablix executou uma consulta de banco de dados para responder esta mensagem.', 'This provider is not configured for native tool calls. Tablix can use server-side fallback execution for database data requests.': 'Este provedor não está configurado para chamadas nativas de ferramentas. Tablix pode usar fallback no servidor para solicitações de dados.', 'Failed': 'Falhou', 'Complete': 'Concluído', 'Running': 'Em execução', 'Message telemetry': 'Telemetria da mensagem', 'Time to first token': 'Tempo até o primeiro token', 'Total streaming time': 'Tempo total de streaming', 'Input tokens': 'Tokens de entrada', 'Output tokens': 'Tokens de saída', 'Total tokens': 'Tokens totais', 'Configured database connections': 'Conexões de banco configuradas', 'ID': 'ID', 'Name': 'Nome', 'Type': 'Tipo', 'Schema': 'Esquema', 'Actions': 'Ações', 'Build Context': 'Gerar contexto', 'Build and Save': 'Gerar e salvar', 'Building...': 'Gerando...', 'Prompt': 'Prompt', 'Context': 'Contexto', 'Table Context': 'Contexto da tabela', 'No context saved.': 'Nenhum contexto salvo.', 'Edit Context': 'Editar contexto', 'Save Context': 'Salvar contexto', 'Saving...': 'Salvando...', 'Context saved.': 'Contexto salvo.', 'Context built and saved.': 'Contexto gerado e salvo.', 'Tables': 'Tabelas', 'Column': 'Coluna', 'Nullable': 'Aceita nulo', 'Default': 'Padrão', 'Status': 'Status', 'Crawled': 'Rastreado', 'Degraded': 'Degradado', 'Last Crawl': 'Último rastreio', 'Error': 'Erro', 'Crawl Status': 'Status do rastreio', 'Preparing crawl.': 'Preparando rastreio.', 'Elapsed': 'Decorrido', 'Current table': 'Tabela atual', 'Relationships': 'Relacionamentos', 'SQL Query': 'Consulta SQL', 'Execute': 'Executar', 'Executing...': 'Executando...', 'No rows returned.': 'Nenhuma linha retornada.', 'Could not connect to server.': 'Não foi possível conectar ao servidor.', 'Query failed.': 'Consulta falhou.', 'Loading settings...': 'Carregando configurações...', 'Save Settings': 'Salvar configurações', 'Persistence': 'Persistência', 'REST and MCP': 'REST e MCP', 'API Keys': 'Chaves API', 'Logging': 'Logs', 'Chat enabled': 'Chat habilitado', 'Default streaming': 'Streaming padrão', 'No default provider': 'Sem provedor padrão', 'System Prompt': 'Prompt do sistema', 'Prompt Processing': 'Processamento de prompt', 'Enabled': 'Habilitado', 'Prefer native tools': 'Preferir ferramentas nativas', 'Execute data requests': 'Executar solicitações de dados', 'Honor SQL-only requests': 'Respeitar solicitações só SQL', 'Retry after schema refresh': 'Tentar novamente após atualizar esquema', 'Chat Tools': 'Ferramentas de chat', 'Tools enabled': 'Ferramentas habilitadas', 'Read-only queries': 'Consultas somente leitura', 'Context updates': 'Atualizações de contexto', 'Restart': 'Reiniciar', 'Model Providers': 'Provedores de modelo', 'Add Model': 'Adicionar modelo', 'Edit Model': 'Editar modelo', 'Test Provider': 'Testar provedor', 'Testing Provider': 'Testando provedor', 'Provider ID': 'ID do provedor', 'Endpoint': 'Endpoint', 'Model': 'Modelo', 'Max Concurrent Requests': 'Solicitações concorrentes máximas', 'Supports native tools': 'Suporta ferramentas nativas', 'Use native tools': 'Usar ferramentas nativas', 'Default Streaming': 'Streaming padrão', 'Strict JSON': 'JSON estrito', 'Temperature': 'Temperatura', 'Top P': 'Top P', 'Max Tokens': 'Tokens máximos', 'Request Timeout': 'Tempo limite da solicitação', 'Clear API key': 'Limpar chave API', 'Save Model': 'Salvar modelo', 'Set Up Tablix': 'Configurar Tablix', 'Model Provider': 'Provedor de modelo', 'Database Context': 'Contexto do banco', 'Crawl Database': 'Rastrear banco', 'Ready for Chat': 'Pronto para chat', 'Save and Continue': 'Salvar e continuar', 'Test Database': 'Testar banco', 'Start Crawl': 'Iniciar rastreio', 'Crawling...': 'Rastreando...', 'Build Database Context': 'Gerar contexto do banco', 'Save Edited Contexts': 'Salvar contextos editados', 'Build Table Contexts': 'Gerar contextos de tabela', 'Go to Chat When Ready': 'Ir ao Chat quando pronto', 'Skip setup': 'Pular configuração', 'Exit setup wizard': 'Sair do assistente', 'Allowed Queries': 'Consultas permitidas', 'Generation Instructions': 'Instruções de geração', 'Table': 'Tabela', 'Generate durable context from the latest crawl with the selected provider.': 'Gere contexto durável do último rastreio com o provedor selecionado.', 'This operation may take some time, please be patient.': 'Esta operação pode levar algum tempo; aguarde.', 'Your provider, database, crawl metadata, and context are stored. Open Chat when you are ready to ask questions.': 'Seu provedor, banco, metadados e contexto foram salvos. Abra o Chat quando quiser perguntar.', 'Tablix is validating this provider with the current settings.': 'Tablix está validando este provedor com as configurações atuais.', 'Hostname': 'Hostname', 'Port': 'Porta', 'User': 'Usuário', 'Password': 'Senha', 'Filename': 'Arquivo', 'Database Name': 'Nome do banco', 'Allowed Queries (comma-separated)': 'Consultas permitidas (separadas por vírgulas)', 'Working...': 'Processando...', 'Delete Database': 'Excluir banco', 'Delete Model': 'Excluir modelo'
  },
  zh: {
    'Databases': '数据库', 'Query': '查询', 'Chat': '聊天', 'Models': '模型', 'Settings': '设置', 'Server URL': '服务器 URL', 'API Key': 'API 密钥', 'Sign In': '登录', 'Add Database': '添加数据库', 'Edit Database': '编辑数据库', 'Save Database': '保存数据库', 'Create Database': '创建数据库', 'Cancel': '取消', 'Confirm': '确认', 'Delete': '删除', 'Edit': '编辑', 'View': '查看', 'Close': '关闭', 'Copy': '复制', 'Refresh': '刷新', 'Download CSV': '下载 CSV', 'Database': '数据库', 'Provider': '提供商', 'Streaming': '流式传输', 'Ask about the selected database.': '询问所选数据库。', 'Responses can include markdown, SQL, tables, and lists.': '回复可以包含 Markdown、SQL、表格和列表。', 'Enter to send, Shift+Enter for newline': 'Enter 发送，Shift+Enter 换行', 'Send': '发送', 'Sending...': '正在发送...', 'API key configured': '已配置 API 密钥', 'No API key': '无 API 密钥', 'Native tools': '原生工具', 'Server fallback': '服务器回退', 'The model did not request a tool call.': '模型未请求工具调用。', 'Tool execution failed; the assistant returned a plain model response.': '工具执行失败；助手返回了普通模型回复。', 'Tablix could not plan a database query for this request.': 'Tablix 无法为此请求规划数据库查询。', 'Tablix ran a database query to answer this message.': 'Tablix 执行了数据库查询来回答此消息。', 'This provider is not configured for native tool calls. Tablix can use server-side fallback execution for database data requests.': '此提供商未配置原生工具调用。Tablix 可对数据库数据请求使用服务器端回退执行。', 'Failed': '失败', 'Complete': '完成', 'Running': '运行中', 'Message telemetry': '消息遥测', 'Time to first token': '首个令牌时间', 'Total streaming time': '总流式时间', 'Input tokens': '输入令牌', 'Output tokens': '输出令牌', 'Total tokens': '总令牌', 'Configured database connections': '已配置的数据库连接', 'ID': 'ID', 'Name': '名称', 'Type': '类型', 'Schema': '架构', 'Actions': '操作', 'Build Context': '生成上下文', 'Build and Save': '生成并保存', 'Building...': '正在生成...', 'Prompt': '提示词', 'Context': '上下文', 'Table Context': '表上下文', 'No context saved.': '未保存上下文。', 'Edit Context': '编辑上下文', 'Save Context': '保存上下文', 'Saving...': '正在保存...', 'Context saved.': '上下文已保存。', 'Context built and saved.': '上下文已生成并保存。', 'Tables': '表', 'Column': '列', 'Nullable': '可为空', 'Default': '默认值', 'Status': '状态', 'Crawled': '已爬取', 'Degraded': '降级', 'Last Crawl': '上次爬取', 'Error': '错误', 'Crawl Status': '爬取状态', 'Preparing crawl.': '正在准备爬取。', 'Elapsed': '已用时间', 'Current table': '当前表', 'Relationships': '关系', 'SQL Query': 'SQL 查询', 'Execute': '执行', 'Executing...': '正在执行...', 'No rows returned.': '未返回行。', 'Could not connect to server.': '无法连接服务器。', 'Query failed.': '查询失败。', 'Loading settings...': '正在加载设置...', 'Save Settings': '保存设置', 'Persistence': '持久化', 'REST and MCP': 'REST 和 MCP', 'API Keys': 'API 密钥', 'Logging': '日志', 'Chat enabled': '启用聊天', 'Default streaming': '默认流式传输', 'No default provider': '无默认提供商', 'System Prompt': '系统提示词', 'Prompt Processing': '提示词处理', 'Enabled': '已启用', 'Prefer native tools': '优先使用原生工具', 'Execute data requests': '执行数据请求', 'Honor SQL-only requests': '遵循仅 SQL 请求', 'Retry after schema refresh': '刷新架构后重试', 'Chat Tools': '聊天工具', 'Tools enabled': '启用工具', 'Read-only queries': '只读查询', 'Context updates': '上下文更新', 'Restart': '重启', 'Model Providers': '模型提供商', 'Add Model': '添加模型', 'Edit Model': '编辑模型', 'Test Provider': '测试提供商', 'Testing Provider': '正在测试提供商', 'Provider ID': '提供商 ID', 'Endpoint': '端点', 'Model': '模型', 'Max Concurrent Requests': '最大并发请求', 'Supports native tools': '支持原生工具', 'Use native tools': '使用原生工具', 'Default Streaming': '默认流式传输', 'Strict JSON': '严格 JSON', 'Temperature': '温度', 'Top P': 'Top P', 'Max Tokens': '最大令牌', 'Request Timeout': '请求超时', 'Clear API key': '清除 API 密钥', 'Save Model': '保存模型', 'Set Up Tablix': '设置 Tablix', 'Model Provider': '模型提供商', 'Database Context': '数据库上下文', 'Crawl Database': '爬取数据库', 'Ready for Chat': '可以聊天', 'Save and Continue': '保存并继续', 'Test Database': '测试数据库', 'Start Crawl': '开始爬取', 'Crawling...': '正在爬取...', 'Build Database Context': '生成数据库上下文', 'Save Edited Contexts': '保存已编辑上下文', 'Build Table Contexts': '生成表上下文', 'Go to Chat When Ready': '准备好后前往聊天', 'Skip setup': '跳过设置', 'Exit setup wizard': '退出设置向导', 'Allowed Queries': '允许的查询', 'Generation Instructions': '生成说明', 'Table': '表', 'Generate durable context from the latest crawl with the selected provider.': '使用所选提供商从最近一次爬取生成持久上下文。', 'This operation may take some time, please be patient.': '此操作可能需要一些时间，请耐心等待。', 'Your provider, database, crawl metadata, and context are stored. Open Chat when you are ready to ask questions.': '提供商、数据库、爬取元数据和上下文已保存。准备好提问时打开聊天。', 'Tablix is validating this provider with the current settings.': 'Tablix 正在使用当前设置验证此提供商。', 'Hostname': '主机名', 'Port': '端口', 'User': '用户', 'Password': '密码', 'Filename': '文件名', 'Database Name': '数据库名称', 'Allowed Queries (comma-separated)': '允许的查询（逗号分隔）', 'Working...': '处理中...', 'Delete Database': '删除数据库', 'Delete Model': '删除模型'
  },
  yue: {
    'Databases': '資料庫', 'Query': '查詢', 'Chat': '聊天', 'Models': '模型', 'Settings': '設定', 'Server URL': '伺服器 URL', 'API Key': 'API 金鑰', 'Sign In': '登入', 'Add Database': '新增資料庫', 'Edit Database': '編輯資料庫', 'Save Database': '儲存資料庫', 'Create Database': '建立資料庫', 'Cancel': '取消', 'Confirm': '確認', 'Delete': '刪除', 'Edit': '編輯', 'View': '檢視', 'Close': '關閉', 'Copy': '複製', 'Refresh': '重新整理', 'Download CSV': '下載 CSV', 'Database': '資料庫', 'Provider': '供應商', 'Streaming': '串流', 'Ask about the selected database.': '問吓已揀嘅資料庫。', 'Responses can include markdown, SQL, tables, and lists.': '回覆可以包含 Markdown、SQL、表格同清單。', 'Enter to send, Shift+Enter for newline': 'Enter 送出，Shift+Enter 換行', 'Send': '送出', 'Sending...': '送出中...', 'API key configured': '已設定 API 金鑰', 'No API key': '冇 API 金鑰', 'Native tools': '原生工具', 'Server fallback': '伺服器後備', 'The model did not request a tool call.': '模型冇要求工具呼叫。', 'Tool execution failed; the assistant returned a plain model response.': '工具執行失敗；助手回傳咗普通模型回覆。', 'Tablix could not plan a database query for this request.': 'Tablix 無法為呢個要求規劃資料庫查詢。', 'Tablix ran a database query to answer this message.': 'Tablix 執行咗資料庫查詢嚟回答呢個訊息。', 'This provider is not configured for native tool calls. Tablix can use server-side fallback execution for database data requests.': '呢個供應商未設定原生工具呼叫。Tablix 可以用伺服器後備執行資料要求。', 'Failed': '失敗', 'Complete': '完成', 'Running': '執行中', 'Message telemetry': '訊息遙測', 'Time to first token': '首個 token 時間', 'Total streaming time': '總串流時間', 'Input tokens': '輸入 tokens', 'Output tokens': '輸出 tokens', 'Total tokens': '總 tokens', 'Configured database connections': '已設定資料庫連線', 'ID': 'ID', 'Name': '名稱', 'Type': '類型', 'Schema': '結構', 'Actions': '動作', 'Build Context': '建立上下文', 'Build and Save': '建立並儲存', 'Building...': '建立中...', 'Prompt': '提示', 'Context': '上下文', 'Table Context': '資料表上下文', 'No context saved.': '未儲存上下文。', 'Edit Context': '編輯上下文', 'Save Context': '儲存上下文', 'Saving...': '儲存中...', 'Context saved.': '上下文已儲存。', 'Context built and saved.': '上下文已建立並儲存。', 'Tables': '資料表', 'Column': '欄位', 'Nullable': '可為空', 'Default': '預設值', 'Status': '狀態', 'Crawled': '已爬取', 'Degraded': '降級', 'Last Crawl': '上次爬取', 'Error': '錯誤', 'Crawl Status': '爬取狀態', 'Preparing crawl.': '準備爬取。', 'Elapsed': '已用時間', 'Current table': '目前資料表', 'Relationships': '關係', 'SQL Query': 'SQL 查詢', 'Execute': '執行', 'Executing...': '執行中...', 'No rows returned.': '冇回傳資料列。', 'Could not connect to server.': '無法連接伺服器。', 'Query failed.': '查詢失敗。', 'Loading settings...': '載入設定中...', 'Save Settings': '儲存設定', 'Persistence': '持久化', 'REST and MCP': 'REST 同 MCP', 'API Keys': 'API 金鑰', 'Logging': '日誌', 'Chat enabled': '啟用聊天', 'Default streaming': '預設串流', 'No default provider': '冇預設供應商', 'System Prompt': '系統提示', 'Prompt Processing': '提示處理', 'Enabled': '已啟用', 'Prefer native tools': '優先原生工具', 'Execute data requests': '執行資料要求', 'Honor SQL-only requests': '遵從只要 SQL 嘅要求', 'Retry after schema refresh': '更新結構後重試', 'Chat Tools': '聊天工具', 'Tools enabled': '工具已啟用', 'Read-only queries': '唯讀查詢', 'Context updates': '上下文更新', 'Restart': '重新啟動', 'Model Providers': '模型供應商', 'Add Model': '新增模型', 'Edit Model': '編輯模型', 'Test Provider': '測試供應商', 'Testing Provider': '測試供應商中', 'Provider ID': '供應商 ID', 'Endpoint': '端點', 'Model': '模型', 'Max Concurrent Requests': '最大並行要求', 'Supports native tools': '支援原生工具', 'Use native tools': '使用原生工具', 'Default Streaming': '預設串流', 'Strict JSON': '嚴格 JSON', 'Temperature': 'Temperature', 'Top P': 'Top P', 'Max Tokens': '最大 tokens', 'Request Timeout': '要求逾時', 'Clear API key': '清除 API 金鑰', 'Save Model': '儲存模型', 'Set Up Tablix': '設定 Tablix', 'Model Provider': '模型供應商', 'Database Context': '資料庫上下文', 'Crawl Database': '爬取資料庫', 'Ready for Chat': '可以聊天', 'Save and Continue': '儲存並繼續', 'Test Database': '測試資料庫', 'Start Crawl': '開始爬取', 'Crawling...': '爬取中...', 'Build Database Context': '建立資料庫上下文', 'Save Edited Contexts': '儲存已編輯上下文', 'Build Table Contexts': '建立資料表上下文', 'Go to Chat When Ready': '準備好就去聊天', 'Skip setup': '略過設定', 'Exit setup wizard': '離開設定精靈', 'Allowed Queries': '允許查詢', 'Generation Instructions': '生成指示', 'Table': '資料表', 'Generate durable context from the latest crawl with the selected provider.': '用已揀供應商由最近爬取生成持久上下文。', 'This operation may take some time, please be patient.': '呢個操作可能需時，請耐心等候。', 'Your provider, database, crawl metadata, and context are stored. Open Chat when you are ready to ask questions.': '供應商、資料庫、爬取 metadata 同上下文已儲存。準備好就開 Chat 問問題。', 'Tablix is validating this provider with the current settings.': 'Tablix 正用目前設定驗證呢個供應商。', 'Hostname': '主機名', 'Port': '連接埠', 'User': '用戶', 'Password': '密碼', 'Filename': '檔案名', 'Database Name': '資料庫名稱', 'Allowed Queries (comma-separated)': '允許查詢（逗號分隔）', 'Working...': '處理中...', 'Delete Database': '刪除資料庫', 'Delete Model': '刪除模型'
  },
  'ja-kanji': {
    'Databases': 'データベース', 'Query': '照会', 'Chat': '対話', 'Models': '模型', 'Settings': '設定', 'Server URL': 'サーバーURL', 'API Key': 'API鍵', 'Sign In': 'サインイン', 'Add Database': 'データベース追加', 'Edit Database': 'データベース編集', 'Save Database': 'データベース保存', 'Create Database': 'データベース作成', 'Cancel': '取消', 'Confirm': '確認', 'Delete': '削除', 'Edit': '編集', 'View': '表示', 'Close': '閉じる', 'Copy': 'コピー', 'Refresh': '更新', 'Download CSV': 'CSVをダウンロード', 'Database': 'データベース', 'Provider': '提供元', 'Streaming': 'ストリーミング', 'Ask about the selected database.': '選択中のデータベースについて質問してください。', 'Responses can include markdown, SQL, tables, and lists.': '回答にはMarkdown、SQL、表、一覧を含められます。', 'Enter to send, Shift+Enter for newline': 'Enterで送信、Shift+Enterで改行', 'Send': '送信', 'Sending...': '送信中...', 'API key configured': 'API鍵設定済み', 'No API key': 'API鍵なし', 'Native tools': 'ネイティブツール', 'Server fallback': 'サーバー代替', 'The model did not request a tool call.': 'モデルはツール呼び出しを要求しませんでした。', 'Tool execution failed; the assistant returned a plain model response.': 'ツール実行に失敗したため、アシスタントは通常のモデル応答を返しました。', 'Tablix could not plan a database query for this request.': 'Tablixはこの要求のデータベース照会を計画できませんでした。', 'Tablix ran a database query to answer this message.': 'Tablixはこのメッセージに答えるためデータベース照会を実行しました。', 'This provider is not configured for native tool calls. Tablix can use server-side fallback execution for database data requests.': 'この提供元はネイティブツール呼び出し用に設定されていません。Tablixはデータ要求にサーバー側代替実行を使用できます。', 'Failed': '失敗', 'Complete': '完了', 'Running': '実行中', 'Message telemetry': 'メッセージ計測', 'Time to first token': '初回トークン時間', 'Total streaming time': '総ストリーミング時間', 'Input tokens': '入力トークン', 'Output tokens': '出力トークン', 'Total tokens': '総トークン', 'Configured database connections': '設定済みデータベース接続', 'ID': 'ID', 'Name': '名称', 'Type': '種類', 'Schema': 'スキーマ', 'Actions': '操作', 'Build Context': 'コンテキスト生成', 'Build and Save': '生成して保存', 'Building...': '生成中...', 'Prompt': 'プロンプト', 'Context': 'コンテキスト', 'Table Context': '表コンテキスト', 'No context saved.': '保存済みコンテキストはありません。', 'Edit Context': 'コンテキスト編集', 'Save Context': 'コンテキスト保存', 'Saving...': '保存中...', 'Context saved.': 'コンテキストを保存しました。', 'Context built and saved.': 'コンテキストを生成して保存しました。', 'Tables': '表', 'Column': '列', 'Nullable': 'Null可', 'Default': '既定値', 'Status': '状態', 'Crawled': 'クロール済み', 'Degraded': '縮退', 'Last Crawl': '最終クロール', 'Error': 'エラー', 'Crawl Status': 'クロール状態', 'Preparing crawl.': 'クロール準備中。', 'Elapsed': '経過', 'Current table': '現在の表', 'Relationships': '関係', 'SQL Query': 'SQL照会', 'Execute': '実行', 'Executing...': '実行中...', 'No rows returned.': '行は返されませんでした。', 'Could not connect to server.': 'サーバーに接続できません。', 'Query failed.': '照会に失敗しました。', 'Loading settings...': '設定読込中...', 'Save Settings': '設定保存', 'Persistence': '永続化', 'REST and MCP': 'RESTとMCP', 'API Keys': 'API鍵', 'Logging': 'ログ', 'Chat enabled': '対話有効', 'Default streaming': '既定ストリーミング', 'No default provider': '既定提供元なし', 'System Prompt': 'システムプロンプト', 'Prompt Processing': 'プロンプト処理', 'Enabled': '有効', 'Prefer native tools': 'ネイティブツール優先', 'Execute data requests': 'データ要求を実行', 'Honor SQL-only requests': 'SQLのみ要求を尊重', 'Retry after schema refresh': 'スキーマ更新後に再試行', 'Chat Tools': '対話ツール', 'Tools enabled': 'ツール有効', 'Read-only queries': '読取専用照会', 'Context updates': 'コンテキスト更新', 'Restart': '再起動', 'Model Providers': 'モデル提供元', 'Add Model': 'モデル追加', 'Edit Model': 'モデル編集', 'Test Provider': '提供元テスト', 'Testing Provider': '提供元テスト中', 'Provider ID': '提供元ID', 'Endpoint': 'エンドポイント', 'Model': 'モデル', 'Max Concurrent Requests': '最大同時要求数', 'Supports native tools': 'ネイティブツール対応', 'Use native tools': 'ネイティブツール使用', 'Default Streaming': '既定ストリーミング', 'Strict JSON': '厳格JSON', 'Temperature': '温度', 'Top P': 'Top P', 'Max Tokens': '最大トークン', 'Request Timeout': '要求タイムアウト', 'Clear API key': 'API鍵を消去', 'Save Model': 'モデル保存', 'Set Up Tablix': 'Tablix設定', 'Model Provider': 'モデル提供元', 'Database Context': 'データベースコンテキスト', 'Crawl Database': 'データベースクロール', 'Ready for Chat': '対話準備完了', 'Save and Continue': '保存して続行', 'Test Database': 'データベーステスト', 'Start Crawl': 'クロール開始', 'Crawling...': 'クロール中...', 'Build Database Context': 'データベースコンテキスト生成', 'Save Edited Contexts': '編集済みコンテキスト保存', 'Build Table Contexts': '表コンテキスト生成', 'Go to Chat When Ready': '準備できたら対話へ', 'Skip setup': '設定をスキップ', 'Exit setup wizard': '設定ウィザード終了', 'Allowed Queries': '許可照会', 'Generation Instructions': '生成指示', 'Table': '表', 'Generate durable context from the latest crawl with the selected provider.': '選択した提供元で最新クロールから永続コンテキストを生成します。', 'This operation may take some time, please be patient.': 'この操作には時間がかかる場合があります。お待ちください。', 'Your provider, database, crawl metadata, and context are stored. Open Chat when you are ready to ask questions.': '提供元、データベース、クロールメタデータ、コンテキストを保存しました。質問する準備ができたら対話を開いてください。', 'Tablix is validating this provider with the current settings.': 'Tablixは現在の設定でこの提供元を検証しています。', 'Hostname': 'ホスト名', 'Port': 'ポート', 'User': 'ユーザー', 'Password': 'パスワード', 'Filename': 'ファイル名', 'Database Name': 'データベース名', 'Allowed Queries (comma-separated)': '許可照会（カンマ区切り）', 'Working...': '処理中...', 'Delete Database': 'データベース削除', 'Delete Model': 'モデル削除'
  },
  ja: {
    'Databases': 'データベース', 'Query': 'クエリ', 'Chat': 'チャット', 'Models': 'モデル', 'Settings': '設定', 'Server URL': 'サーバー URL', 'API Key': 'API キー', 'Sign In': 'サインイン', 'Add Database': 'データベースを追加', 'Edit Database': 'データベースを編集', 'Save Database': 'データベースを保存', 'Create Database': 'データベースを作成', 'Cancel': 'キャンセル', 'Confirm': '確認', 'Delete': '削除', 'Edit': '編集', 'View': '表示', 'Close': '閉じる', 'Copy': 'コピー', 'Refresh': '更新', 'Download CSV': 'CSV をダウンロード', 'Database': 'データベース', 'Provider': 'プロバイダー', 'Streaming': 'ストリーミング', 'Ask about the selected database.': '選択したデータベースについて質問してください。', 'Responses can include markdown, SQL, tables, and lists.': '回答には Markdown、SQL、表、リストを含められます。', 'Enter to send, Shift+Enter for newline': 'Enter で送信、Shift+Enter で改行', 'Send': '送信', 'Sending...': '送信中...', 'API key configured': 'API キー設定済み', 'No API key': 'API キーなし', 'Native tools': 'ネイティブツール', 'Server fallback': 'サーバーフォールバック', 'The model did not request a tool call.': 'モデルはツール呼び出しを要求しませんでした。', 'Tool execution failed; the assistant returned a plain model response.': 'ツール実行に失敗したため、アシスタントは通常のモデル応答を返しました。', 'Tablix could not plan a database query for this request.': 'Tablix はこのリクエストのデータベースクエリを計画できませんでした。', 'Tablix ran a database query to answer this message.': 'Tablix はこのメッセージに回答するためにデータベースクエリを実行しました。', 'This provider is not configured for native tool calls. Tablix can use server-side fallback execution for database data requests.': 'このプロバイダーはネイティブツール呼び出し用に設定されていません。Tablix はデータ要求にサーバー側フォールバック実行を使用できます。', 'Failed': '失敗', 'Complete': '完了', 'Running': '実行中', 'Message telemetry': 'メッセージテレメトリ', 'Time to first token': '最初のトークンまでの時間', 'Total streaming time': '合計ストリーミング時間', 'Input tokens': '入力トークン', 'Output tokens': '出力トークン', 'Total tokens': '合計トークン', 'Configured database connections': '設定済みデータベース接続', 'ID': 'ID', 'Name': '名前', 'Type': '種類', 'Schema': 'スキーマ', 'Actions': '操作', 'Build Context': 'コンテキストを生成', 'Build and Save': '生成して保存', 'Building...': '生成中...', 'Prompt': 'プロンプト', 'Context': 'コンテキスト', 'Table Context': 'テーブルコンテキスト', 'No context saved.': '保存されたコンテキストはありません。', 'Edit Context': 'コンテキストを編集', 'Save Context': 'コンテキストを保存', 'Saving...': '保存中...', 'Context saved.': 'コンテキストを保存しました。', 'Context built and saved.': 'コンテキストを生成して保存しました。', 'Tables': 'テーブル', 'Column': '列', 'Nullable': 'NULL 可', 'Default': '既定値', 'Status': '状態', 'Crawled': 'クロール済み', 'Degraded': '縮退', 'Last Crawl': '最終クロール', 'Error': 'エラー', 'Crawl Status': 'クロール状態', 'Preparing crawl.': 'クロールを準備中です。', 'Elapsed': '経過', 'Current table': '現在のテーブル', 'Relationships': 'リレーション', 'SQL Query': 'SQL クエリ', 'Execute': '実行', 'Executing...': '実行中...', 'No rows returned.': '行は返されませんでした。', 'Could not connect to server.': 'サーバーに接続できませんでした。', 'Query failed.': 'クエリに失敗しました。', 'Loading settings...': '設定を読み込み中...', 'Save Settings': '設定を保存', 'Persistence': '永続化', 'REST and MCP': 'REST と MCP', 'API Keys': 'API キー', 'Logging': 'ログ', 'Chat enabled': 'チャット有効', 'Default streaming': '既定のストリーミング', 'No default provider': '既定のプロバイダーなし', 'System Prompt': 'システムプロンプト', 'Prompt Processing': 'プロンプト処理', 'Enabled': '有効', 'Prefer native tools': 'ネイティブツールを優先', 'Execute data requests': 'データ要求を実行', 'Honor SQL-only requests': 'SQL のみの要求を尊重', 'Retry after schema refresh': 'スキーマ更新後に再試行', 'Chat Tools': 'チャットツール', 'Tools enabled': 'ツール有効', 'Read-only queries': '読み取り専用クエリ', 'Context updates': 'コンテキスト更新', 'Restart': '再起動', 'Model Providers': 'モデルプロバイダー', 'Add Model': 'モデルを追加', 'Edit Model': 'モデルを編集', 'Test Provider': 'プロバイダーをテスト', 'Testing Provider': 'プロバイダーをテスト中', 'Provider ID': 'プロバイダー ID', 'Endpoint': 'エンドポイント', 'Model': 'モデル', 'Max Concurrent Requests': '最大同時リクエスト数', 'Supports native tools': 'ネイティブツール対応', 'Use native tools': 'ネイティブツールを使用', 'Default Streaming': '既定のストリーミング', 'Strict JSON': '厳密な JSON', 'Temperature': '温度', 'Top P': 'Top P', 'Max Tokens': '最大トークン', 'Request Timeout': 'リクエストタイムアウト', 'Clear API key': 'API キーを消去', 'Save Model': 'モデルを保存', 'Set Up Tablix': 'Tablix を設定', 'Model Provider': 'モデルプロバイダー', 'Database Context': 'データベースコンテキスト', 'Crawl Database': 'データベースをクロール', 'Ready for Chat': 'チャットの準備完了', 'Save and Continue': '保存して続行', 'Test Database': 'データベースをテスト', 'Start Crawl': 'クロール開始', 'Crawling...': 'クロール中...', 'Build Database Context': 'データベースコンテキストを生成', 'Save Edited Contexts': '編集したコンテキストを保存', 'Build Table Contexts': 'テーブルコンテキストを生成', 'Go to Chat When Ready': '準備できたらチャットへ', 'Skip setup': '設定をスキップ', 'Exit setup wizard': '設定ウィザードを終了', 'Allowed Queries': '許可クエリ', 'Generation Instructions': '生成指示', 'Table': 'テーブル', 'Generate durable context from the latest crawl with the selected provider.': '選択したプロバイダーで最新クロールから永続コンテキストを生成します。', 'This operation may take some time, please be patient.': 'この操作には時間がかかる場合があります。しばらくお待ちください。', 'Your provider, database, crawl metadata, and context are stored. Open Chat when you are ready to ask questions.': 'プロバイダー、データベース、クロールメタデータ、コンテキストが保存されました。質問する準備ができたら Chat を開いてください。', 'Tablix is validating this provider with the current settings.': 'Tablix は現在の設定でこのプロバイダーを検証しています。', 'Hostname': 'ホスト名', 'Port': 'ポート', 'User': 'ユーザー', 'Password': 'パスワード', 'Filename': 'ファイル名', 'Database Name': 'データベース名', 'Allowed Queries (comma-separated)': '許可クエリ（カンマ区切り）', 'Working...': '処理中...', 'Delete Database': 'データベースを削除', 'Delete Model': 'モデルを削除'
  },
  fa: {
    'Databases': 'پایگاه‌های داده', 'Query': 'پرس‌وجو', 'Chat': 'گفتگو', 'Models': 'مدل‌ها', 'Settings': 'تنظیمات', 'Server URL': 'نشانی سرور', 'API Key': 'کلید API', 'Sign In': 'ورود', 'Add Database': 'افزودن پایگاه داده', 'Edit Database': 'ویرایش پایگاه داده', 'Save Database': 'ذخیره پایگاه داده', 'Create Database': 'ایجاد پایگاه داده', 'Cancel': 'لغو', 'Confirm': 'تأیید', 'Delete': 'حذف', 'Edit': 'ویرایش', 'View': 'مشاهده', 'Close': 'بستن', 'Copy': 'کپی', 'Refresh': 'بازخوانی', 'Download CSV': 'دانلود CSV', 'Database': 'پایگاه داده', 'Provider': 'ارائه‌دهنده', 'Streaming': 'جریان‌دهی', 'Ask about the selected database.': 'درباره پایگاه داده انتخاب‌شده سؤال کنید.', 'Responses can include markdown, SQL, tables, and lists.': 'پاسخ‌ها می‌توانند شامل Markdown، SQL، جدول و فهرست باشند.', 'Enter to send, Shift+Enter for newline': 'Enter برای ارسال، Shift+Enter برای خط جدید', 'Send': 'ارسال', 'Sending...': 'در حال ارسال...', 'API key configured': 'کلید API تنظیم شده است', 'No API key': 'بدون کلید API', 'Native tools': 'ابزارهای بومی', 'Server fallback': 'جایگزین سرور', 'The model did not request a tool call.': 'مدل درخواست فراخوانی ابزار نکرد.', 'Tool execution failed; the assistant returned a plain model response.': 'اجرای ابزار ناموفق بود؛ دستیار یک پاسخ ساده مدل برگرداند.', 'Tablix could not plan a database query for this request.': 'Tablix نتوانست برای این درخواست یک پرس‌وجوی پایگاه داده برنامه‌ریزی کند.', 'Tablix ran a database query to answer this message.': 'Tablix برای پاسخ به این پیام یک پرس‌وجوی پایگاه داده اجرا کرد.', 'This provider is not configured for native tool calls. Tablix can use server-side fallback execution for database data requests.': 'این ارائه‌دهنده برای فراخوانی ابزار بومی تنظیم نشده است. Tablix می‌تواند برای درخواست‌های داده از اجرای جایگزین سمت سرور استفاده کند.', 'Failed': 'ناموفق', 'Complete': 'کامل', 'Running': 'در حال اجرا', 'Message telemetry': 'دورسنجی پیام', 'Time to first token': 'زمان تا نخستین توکن', 'Total streaming time': 'کل زمان جریان‌دهی', 'Input tokens': 'توکن‌های ورودی', 'Output tokens': 'توکن‌های خروجی', 'Total tokens': 'کل توکن‌ها', 'Configured database connections': 'اتصال‌های پایگاه داده تنظیم‌شده', 'ID': 'شناسه', 'Name': 'نام', 'Type': 'نوع', 'Schema': 'اسکیما', 'Actions': 'اقدام‌ها', 'Build Context': 'ساخت زمینه', 'Build and Save': 'ساخت و ذخیره', 'Building...': 'در حال ساخت...', 'Prompt': 'پرامپت', 'Context': 'زمینه', 'Table Context': 'زمینه جدول', 'No context saved.': 'زمینه‌ای ذخیره نشده است.', 'Edit Context': 'ویرایش زمینه', 'Save Context': 'ذخیره زمینه', 'Saving...': 'در حال ذخیره...', 'Context saved.': 'زمینه ذخیره شد.', 'Context built and saved.': 'زمینه ساخته و ذخیره شد.', 'Tables': 'جدول‌ها', 'Column': 'ستون', 'Nullable': 'پذیرش Null', 'Default': 'پیش‌فرض', 'Status': 'وضعیت', 'Crawled': 'خزش شده', 'Degraded': 'کاهش‌یافته', 'Last Crawl': 'آخرین خزش', 'Error': 'خطا', 'Crawl Status': 'وضعیت خزش', 'Preparing crawl.': 'در حال آماده‌سازی خزش.', 'Elapsed': 'سپری‌شده', 'Current table': 'جدول فعلی', 'Relationships': 'رابطه‌ها', 'SQL Query': 'پرس‌وجوی SQL', 'Execute': 'اجرا', 'Executing...': 'در حال اجرا...', 'No rows returned.': 'هیچ ردیفی برگردانده نشد.', 'Could not connect to server.': 'اتصال به سرور ممکن نشد.', 'Query failed.': 'پرس‌وجو ناموفق بود.', 'Loading settings...': 'در حال بارگذاری تنظیمات...', 'Save Settings': 'ذخیره تنظیمات', 'Persistence': 'ماندگاری', 'REST and MCP': 'REST و MCP', 'API Keys': 'کلیدهای API', 'Logging': 'گزارش‌گیری', 'Chat enabled': 'گفتگو فعال است', 'Default streaming': 'جریان‌دهی پیش‌فرض', 'No default provider': 'بدون ارائه‌دهنده پیش‌فرض', 'System Prompt': 'پرامپت سیستم', 'Prompt Processing': 'پردازش پرامپت', 'Enabled': 'فعال', 'Prefer native tools': 'ترجیح ابزارهای بومی', 'Execute data requests': 'اجرای درخواست‌های داده', 'Honor SQL-only requests': 'رعایت درخواست‌های فقط SQL', 'Retry after schema refresh': 'تلاش دوباره پس از تازه‌سازی اسکیما', 'Chat Tools': 'ابزارهای گفتگو', 'Tools enabled': 'ابزارها فعال‌اند', 'Read-only queries': 'پرس‌وجوهای فقط خواندنی', 'Context updates': 'به‌روزرسانی‌های زمینه', 'Restart': 'راه‌اندازی مجدد', 'Model Providers': 'ارائه‌دهندگان مدل', 'Add Model': 'افزودن مدل', 'Edit Model': 'ویرایش مدل', 'Test Provider': 'آزمون ارائه‌دهنده', 'Testing Provider': 'در حال آزمون ارائه‌دهنده', 'Provider ID': 'شناسه ارائه‌دهنده', 'Endpoint': 'نقطه پایانی', 'Model': 'مدل', 'Max Concurrent Requests': 'بیشینه درخواست همزمان', 'Supports native tools': 'پشتیبانی از ابزار بومی', 'Use native tools': 'استفاده از ابزار بومی', 'Default Streaming': 'جریان‌دهی پیش‌فرض', 'Strict JSON': 'JSON سخت‌گیرانه', 'Temperature': 'دما', 'Top P': 'Top P', 'Max Tokens': 'بیشینه توکن‌ها', 'Request Timeout': 'مهلت درخواست', 'Clear API key': 'پاک کردن کلید API', 'Save Model': 'ذخیره مدل', 'Set Up Tablix': 'راه‌اندازی Tablix', 'Model Provider': 'ارائه‌دهنده مدل', 'Database Context': 'زمینه پایگاه داده', 'Crawl Database': 'خزش پایگاه داده', 'Ready for Chat': 'آماده گفتگو', 'Save and Continue': 'ذخیره و ادامه', 'Test Database': 'آزمون پایگاه داده', 'Start Crawl': 'شروع خزش', 'Crawling...': 'در حال خزش...', 'Build Database Context': 'ساخت زمینه پایگاه داده', 'Save Edited Contexts': 'ذخیره زمینه‌های ویرایش‌شده', 'Build Table Contexts': 'ساخت زمینه‌های جدول', 'Go to Chat When Ready': 'پس از آماده شدن به گفتگو بروید', 'Skip setup': 'رد کردن راه‌اندازی', 'Exit setup wizard': 'خروج از راه‌انداز', 'Allowed Queries': 'پرس‌وجوهای مجاز', 'Generation Instructions': 'دستورالعمل‌های تولید', 'Table': 'جدول', 'Generate durable context from the latest crawl with the selected provider.': 'با ارائه‌دهنده انتخاب‌شده از آخرین خزش، زمینه پایدار تولید کنید.', 'This operation may take some time, please be patient.': 'این عملیات ممکن است زمان‌بر باشد؛ لطفاً صبور باشید.', 'Your provider, database, crawl metadata, and context are stored. Open Chat when you are ready to ask questions.': 'ارائه‌دهنده، پایگاه داده، فراداده خزش و زمینه ذخیره شده‌اند. هر زمان آماده بودید Chat را باز کنید.', 'Tablix is validating this provider with the current settings.': 'Tablix در حال اعتبارسنجی این ارائه‌دهنده با تنظیمات فعلی است.', 'Hostname': 'نام میزبان', 'Port': 'درگاه', 'User': 'کاربر', 'Password': 'گذرواژه', 'Filename': 'نام فایل', 'Database Name': 'نام پایگاه داده', 'Allowed Queries (comma-separated)': 'پرس‌وجوهای مجاز (جداشده با ویرگول)', 'Working...': 'در حال کار...', 'Delete Database': 'حذف پایگاه داده', 'Delete Model': 'حذف مدل'
  }
};

const tooltips: Record<string, string> = {
  'nav.brand': 'Open the database workspace.',
  'nav.databases': 'Manage database connections, crawl schemas, and build durable context.',
  'nav.query': 'Run one permitted SQL statement and inspect or export the returned rows.',
  'nav.chat': 'Ask natural-language questions against the selected database and model provider.',
  'nav.models': 'Configure model endpoints used by Chat and context generation.',
  'nav.settings': 'Edit server settings that are safe to change from the dashboard.',
  'nav.server': 'The backend URL this dashboard is configured to display and proxy API calls through.',
  'nav.github': 'Open the Tablix source repository in a new browser tab.',
  'nav.theme': 'Switch the dashboard color scheme for this browser.',
  'nav.language': 'Choose the language used for dashboard text, help, and tooltips.',
  'nav.logout': 'Clear the current API key session and return to login.',
  'actions.open': 'Open row-specific actions without navigating away from the table.',
  'actions.edit': 'Edit this record in a modal or form.',
  'actions.test': 'Validate connectivity using the saved provider or database settings.',
  'actions.delete': 'Permanently remove this record from Tablix product state.',
  'actions.buildContext': 'Generate durable database context using the most recent successful crawl.',
  'models.add': 'Create a new model provider record stored in tablix.db.',
  'models.id': 'Stable provider identifier used by chat, setup, REST, and settings defaults.',
  'models.name': 'Human-readable provider name shown in dropdowns and logs.',
  'models.type': 'Provider protocol adapter PolyPrompt should use for this endpoint.',
  'models.endpoint': 'Base URL reachable from the Tablix server container or process.',
  'models.model': 'Model name sent to the provider for chat and context generation.',
  'models.apiKey': 'Provider authentication secret. Leave blank on edit to preserve the saved value.',
  'models.enabled': 'Controls whether this provider appears in Chat and setup workflows.',
  'models.streaming': 'Prefer streaming responses by default when this provider is selected.',
  'models.supportsTools': 'Indicates the selected provider/model is expected to emit native tool calls.',
  'models.useTools': 'Allow Tablix to send native tool definitions to this provider during chat.',
  'models.strictJson': 'Marks whether fallback planner responses are expected to follow strict JSON.',
  'models.clearKey': 'Remove the saved API key when this provider is saved.',
  'models.temperature': 'Lower values produce steadier answers; higher values increase variation.',
  'models.topP': 'Optional nucleus sampling limit. Leave blank to use the provider default.',
  'models.maxTokens': 'Maximum output size for model responses. Leave blank for provider defaults.',
  'models.timeout': 'Timeout for one provider request, not an entire multi-table batch.',
  'models.concurrency': 'Maximum provider requests Tablix may run in parallel for batch context generation.',
  'models.systemPrompt': 'Optional provider-specific system prompt. When set, it replaces the Settings system prompt for this provider.',
  'models.save': 'Persist the model provider changes to tablix.db.',
  'models.cancel': 'Close without saving model provider changes.',
  'models.modalTest': 'Send a small validation request using the current modal values.',
  'chat.database': 'Database whose schema, context, permissions, and contents constrain this conversation.',
  'chat.provider': 'Model provider that will generate the assistant response.',
  'chat.streaming': 'Receive assistant text as chunks when the active execution path supports streaming.',
  'chat.input': 'Ask a question about the selected database. Enter sends; Shift+Enter inserts a newline.',
  'chat.send': 'Send the current message to the selected database chat provider.',
  'settings.systemPrompt': 'Global default prompt used unless a selected model provider has its own override.',
  'common.close': 'Close this dialog without continuing the current action.',
  'common.refresh': 'Reload the latest data from the server.',
  'common.previous': 'Move to the previous page of results.',
  'common.next': 'Move to the next page of results.',
  'common.pageJump': 'Jump directly to a page number in this result set.',
  'common.copy': 'Copy this value to the clipboard.',
  'common.download': 'Download the displayed result data.',
  'generic.button': 'Activate this control in the current workflow: {label}.',
  'generic.link': 'Navigate to or open this dashboard destination: {label}.',
  'generic.input': 'Edit this value; changes apply when the surrounding form or action is saved.',
  'generic.select': 'Choose one available option for this workflow.',
  'generic.checkbox': 'Toggle this setting on or off for the current workflow.',
  'generic.textarea': 'Enter multi-line text for this workflow; review before saving or sending.',
  'generic.control': 'Use this dashboard control in the current workflow.'
};

const languageSet = new Set<DashboardLanguage>(dashboardLanguages.map(language => language.Code));

export function getLanguage(): DashboardLanguage {
  const stored = localStorage.getItem(languageKey) as DashboardLanguage | null;
  return stored && languageSet.has(stored) ? stored : 'en';
}

export function setLanguage(language: DashboardLanguage) {
  localStorage.setItem(languageKey, language);
  document.documentElement.lang = language;
  document.documentElement.dir = getLanguageDirection(language);
  window.dispatchEvent(new CustomEvent('tablix-language-changed'));
}

export function getLanguageDirection(language: DashboardLanguage = getLanguage()) {
  return dashboardLanguages.find(option => option.Code === language)?.Direction || 'ltr';
}

export function translateText(text: string, language: DashboardLanguage = getLanguage()) {
  if (!text) return text;
  const normalized = normalizeText(text);
  const canonical = findCanonicalText(normalized);
  if (!canonical) return text;
  return phrases[language][canonical] || canonical;
}

export function translateTooltip(key: string, language: DashboardLanguage = getLanguage()) {
  const canonical = tooltips[key] || key;
  return translateText(canonical, language);
}

export function formatLocalizedTooltip(key: string, label: string, language: DashboardLanguage = getLanguage()) {
  const canonical = tooltips[key] || key;
  const translated = translateText(canonical, language);
  const translatedLabel = translateText(label, language);
  return translated.replace('{label}', translatedLabel || label || translateText('control', language));
}

export function translateAttributeValue(value: string, language: DashboardLanguage = getLanguage()) {
  const exact = translateText(value, language);
  if (exact !== value) return exact;

  const normalized = normalizeText(value);
  const percent = normalized.match(/^(\d+)% complete$/);
  if (percent) return localizeTemplate('{value}% complete', percent[1], language);

  const elapsed = normalized.match(/^Elapsed: (.+)$/);
  if (elapsed) return translateText('Elapsed', language) + ': ' + elapsed[1];

  const currentTable = normalized.match(/^Current table: (.+)$/);
  if (currentTable) return translateText('Current table', language) + ': ' + currentTable[1];

  const tables = normalized.match(/^Tables: (.+)$/);
  if (tables) return translateText('Tables', language) + ': ' + tables[1];

  const relationships = normalized.match(/^Relationships: (.+)$/);
  if (relationships) return translateText('Relationships', language) + ': ' + relationships[1];

  return value;
}

export function translateVisibleText(value: string, language: DashboardLanguage = getLanguage()) {
  const exact = translateText(value, language);
  if (exact !== value) return exact;
  return translateDynamicText(value, language);
}

function translateDynamicText(value: string, language: DashboardLanguage) {
  const normalized = normalizeText(value);

  const step = normalized.match(/^Step (\d+) of (\d+)$/);
  if (step) return localizeStep(step[1], step[2], language);

  const page = normalized.match(/^Page (\d+) of (\d+)$/);
  if (page) return localizePage(page[1], page[2], language);

  const tableProgress = normalized.match(/^Table (\d+) of (\d+)$/);
  if (tableProgress) return localizeTableProgress(tableProgress[1], tableProgress[2], language);

  const rows = normalized.match(/^(\d+) row\(s\)$/);
  if (rows) return localizeCount(rows[1], 'row', language);

  const records = normalized.match(/^(\d+) records?$/);
  if (records) return localizeCount(records[1], 'record', language);

  const elapsed = normalized.match(/^Elapsed: (.+)$/);
  if (elapsed) return translateText('Elapsed', language) + ': ' + elapsed[1];

  const currentTable = normalized.match(/^Current table: (.+)$/);
  if (currentTable) return translateText('Current table', language) + ': ' + currentTable[1];

  const tables = normalized.match(/^Tables: (.+)$/);
  if (tables) return translateText('Tables', language) + ': ' + tables[1];

  const relationships = normalized.match(/^Relationships: (.+)$/);
  if (relationships) return translateText('Relationships', language) + ': ' + relationships[1];

  const ttft = normalized.match(/^TTFT: (.+)$/);
  if (ttft) return translateText('Time to first token', language) + ': ' + ttft[1];

  const total = normalized.match(/^Total: (.+)$/);
  if (total) return translateText('Total streaming time', language) + ': ' + total[1];

  const inputTokens = normalized.match(/^Input tokens: (.+)$/);
  if (inputTokens) return translateText('Input tokens', language) + ': ' + inputTokens[1];

  const outputTokens = normalized.match(/^Output tokens: (.+)$/);
  if (outputTokens) return translateText('Output tokens', language) + ': ' + outputTokens[1];

  const totalTokens = normalized.match(/^Total tokens: (.+)$/);
  if (totalTokens) return translateText('Total tokens', language) + ': ' + totalTokens[1];

  return value;
}

function findCanonicalText(text: string) {
  if (phrases.en[text]) return text;

  for (const language of dashboardLanguages) {
    const entries = phrases[language.Code];
    for (const canonical of Object.keys(phrases.en)) {
      if (entries[canonical] === text) return canonical;
    }
  }

  return null;
}

function normalizeText(text: string) {
  return text.replace(/\s+/g, ' ').trim();
}

function localizeTemplate(template: string, value: string, language: DashboardLanguage) {
  if (language === 'fa') return value + '٪ کامل';
  if (language === 'zh') return value + '% 完成';
  if (language === 'yue') return value + '% 完成';
  if (language === 'ja' || language === 'ja-kanji') return value + '% 完了';
  if (language === 'es') return value + '% completo';
  if (language === 'fr') return value + ' % terminé';
  if (language === 'it') return value + '% completato';
  if (language === 'pt') return value + '% concluído';
  return template.replace('{value}', value);
}

function localizeStep(current: string, total: string, language: DashboardLanguage) {
  if (language === 'fa') return 'مرحله ' + current + ' از ' + total;
  if (language === 'zh') return '第 ' + current + ' 步，共 ' + total + ' 步';
  if (language === 'yue') return '第 ' + current + ' 步，共 ' + total + ' 步';
  if (language === 'ja' || language === 'ja-kanji') return current + ' / ' + total + ' ステップ';
  if (language === 'es') return 'Paso ' + current + ' de ' + total;
  if (language === 'fr') return 'Étape ' + current + ' sur ' + total;
  if (language === 'it') return 'Passaggio ' + current + ' di ' + total;
  if (language === 'pt') return 'Etapa ' + current + ' de ' + total;
  return 'Step ' + current + ' of ' + total;
}

function localizePage(current: string, total: string, language: DashboardLanguage) {
  if (language === 'fa') return 'صفحه ' + current + ' از ' + total;
  if (language === 'zh') return '第 ' + current + ' 页，共 ' + total + ' 页';
  if (language === 'yue') return '第 ' + current + ' 頁，共 ' + total + ' 頁';
  if (language === 'ja' || language === 'ja-kanji') return current + ' / ' + total + ' ページ';
  if (language === 'es') return 'Página ' + current + ' de ' + total;
  if (language === 'fr') return 'Page ' + current + ' sur ' + total;
  if (language === 'it') return 'Pagina ' + current + ' di ' + total;
  if (language === 'pt') return 'Página ' + current + ' de ' + total;
  return 'Page ' + current + ' of ' + total;
}

function localizeTableProgress(current: string, total: string, language: DashboardLanguage) {
  if (language === 'fa') return 'جدول ' + current + ' از ' + total;
  if (language === 'zh') return '第 ' + current + ' 个表，共 ' + total + ' 个';
  if (language === 'yue') return '第 ' + current + ' 個資料表，共 ' + total + ' 個';
  if (language === 'ja' || language === 'ja-kanji') return '表 ' + current + ' / ' + total;
  if (language === 'es') return 'Tabla ' + current + ' de ' + total;
  if (language === 'fr') return 'Table ' + current + ' sur ' + total;
  if (language === 'it') return 'Tabella ' + current + ' di ' + total;
  if (language === 'pt') return 'Tabela ' + current + ' de ' + total;
  return 'Table ' + current + ' of ' + total;
}

function localizeCount(count: string, unit: string, language: DashboardLanguage) {
  if (unit === 'row') {
    if (language === 'fa') return count + ' ردیف';
    if (language === 'zh') return count + ' 行';
    if (language === 'yue') return count + ' 資料列';
    if (language === 'ja' || language === 'ja-kanji') return count + ' 行';
    if (language === 'es') return count + ' fila(s)';
    if (language === 'fr') return count + ' ligne(s)';
    if (language === 'it') return count + ' riga/righe';
    if (language === 'pt') return count + ' linha(s)';
  }

  if (language === 'fa') return count + ' رکورد';
  if (language === 'zh') return count + ' 条记录';
  if (language === 'yue') return count + ' 筆記錄';
  if (language === 'ja' || language === 'ja-kanji') return count + ' レコード';
  if (language === 'es') return count + ' registro(s)';
  if (language === 'fr') return count + ' enregistrement(s)';
  if (language === 'it') return count + ' record';
  if (language === 'pt') return count + ' registro(s)';
  return count + ' ' + unit + '(s)';
}
