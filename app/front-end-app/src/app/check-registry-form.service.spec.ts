import { TestBed } from '@angular/core/testing';

import { CheckRegistryFormService } from './check-registry-form.service';

describe('CheckRegistryFormService', () => {
  let service: CheckRegistryFormService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(CheckRegistryFormService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});
