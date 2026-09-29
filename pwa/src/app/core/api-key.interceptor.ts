import { HttpInterceptorFn } from '@angular/common/http';
import { environment } from '../../environments/environment';

/**
 * Anexa o header X-Api-Key nas chamadas pra API, quando uma chave estiver configurada.
 * Se `environment.apiKey` estiver vazio (padrão em desenvolvimento local), a requisição
 * segue sem alteração, exatamente como antes desse interceptor existir.
 */
export const apiKeyInterceptor: HttpInterceptorFn = (req, next) => {
  if (!environment.apiKey || !req.url.startsWith(environment.apiBaseUrl)) {
    return next(req);
  }

  return next(
    req.clone({
      setHeaders: { 'X-Api-Key': environment.apiKey },
    })
  );
};
