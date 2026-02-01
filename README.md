# CurrencyService

This project is a **Currency Exchange System** implemented using a **client–server architecture**.  
It was developed as part of a university assignment to demonstrate communication between a client application, a web service, and a database.

---

## 📌 Project Description

The system allows users to:
- Create an account (registration)
- Top up their virtual balance
- Buy and sell currencies
- Check account balance
- Store and view transaction data

The application uses a **SOAP-based WCF Web Service** for communication and **SQL Server** for persistent data storage.

---

## 🧱 System Architecture

The solution consists of three main projects:

- **CurrencyClient**  
  Console client that sends requests to the service (simulates a mobile application).

- **CurrencyService**  
  WCF service containing business logic and database operations.

- **CurrencyServiceHost**  
  Console application that hosts and runs the WCF service.

---

## 🗄️ Database

- **Database:** SQL Server  
- **Tables include:** Users, Accounts, CurrencyHoldings, Transactions  
- Stores user data, balances, and transaction history.

---

## ⚙️ Technologies Used

- C# (.NET Framework 4.7.2)
- WCF (SOAP)
- SQL Server
- Visual Studio
- Git & GitHub

---

## ▶️ How to Run the Project

1. Clone the repository:
   ```bash
   git clone https://github.com/MajidovAvaz/CurrencyService.git
1. **Open Visual Studio as Administrator**
2. Open the solution file:
3. Set startup projects:
- `CurrencyServiceHost` → Start
- `CurrencyClient` → Start
4. Run the solution (**Ctrl + F5**)

You should see:
- The service host starting successfully
- The client displaying output and interacting with the service

---

## 🔐 Common Issue & Fix

### ❌ Error: Access is denied when opening HTTP endpoint
**Cause:** Windows blocks non-admin processes from binding to HTTP ports.

**Fix options:**
- Run Visual Studio as **Administrator**

