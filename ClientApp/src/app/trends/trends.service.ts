import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { TrendAnalysisResponse } from '../shared/models';

@Injectable({ providedIn: 'root' })
export class TrendsService {
  private readonly http = inject(HttpClient);

  analyze(fromDate: string, toDate: string, aggregateBy: 'amount' | 'count'): Observable<TrendAnalysisResponse> {
    return this.http.post<TrendAnalysisResponse>('/api/trends/analyze', { fromDate, toDate, aggregateBy });
  }
}
