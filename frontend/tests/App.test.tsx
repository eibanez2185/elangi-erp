import { render, screen } from '@testing-library/react'
import '@testing-library/jest-dom/vitest'
import { expect, test } from 'vitest'
import App from '../src/App'

test('renders the ELANGI ERP application shell', () => {
  render(<App />)

  expect(screen.getByRole('heading', { name: 'ELANGI ERP' })).toBeInTheDocument()
})
