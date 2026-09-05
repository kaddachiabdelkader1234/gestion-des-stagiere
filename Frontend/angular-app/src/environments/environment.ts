export const environment = {
  production: false,
  // API Gateway (Backend/api-gateway) binds 18080. All calls go through it — never
  // directly to a service port.
  apiUrl: 'http://localhost:18080/api/v1',
  authApiUrl: 'http://localhost:18080/api/v1/auth'
};
