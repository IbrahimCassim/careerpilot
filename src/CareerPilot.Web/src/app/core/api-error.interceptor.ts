import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { catchError, throwError } from 'rxjs';

export const apiErrorInterceptor: HttpInterceptorFn = (request, next) => next(request).pipe(
  catchError((error: HttpErrorResponse) => {
    if (error.status === 401 && !location.pathname.startsWith('/api/auth')) location.assign('/api/auth/login?returnUrl=' + encodeURIComponent(location.pathname));
    return throwError(() => error);
  })
);
