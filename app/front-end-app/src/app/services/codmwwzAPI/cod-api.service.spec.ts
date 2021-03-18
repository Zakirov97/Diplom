import { TestBed } from '@angular/core/testing';

import { CodAPIService } from './cod-api.service';

describe('CodAPIService', () => {
  let service: CodAPIService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(CodAPIService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});
