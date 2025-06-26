// Application configuration utility
export const config = {
  // API Configuration
  apiBaseUrl: import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000/api',
  
  // Application Information
  appName: import.meta.env.VITE_APP_NAME || 'Scheduling App',
  appVersion: import.meta.env.VITE_APP_VERSION || '0.0.0',
  
  // Environment
  isDevelopment: import.meta.env.DEV,
  isProduction: import.meta.env.PROD,
  debugMode: import.meta.env.VITE_DEBUG === 'true',
  
  // Build Information
  buildTime: new Date().toISOString(),
} as const;
