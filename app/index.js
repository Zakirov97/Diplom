const express = require('express');
const cors = require('cors');//Корректное подключение других сайтов к нам API крч говоря
const bodyParser = require('body-parser');
const mongoose = require('mongoose');//Подключение к бд
const passport = require('passport');
const path = require('path');
const config = require('./config/db');
const account = require('./routes/account');
const leaderboard = require('./routes/leaderboard');

const app = express();//Запускаем наше приложение

//const port = 3000;
const port = process.env.PORT || 8080;

app.use(passport.initialize());
app.use(passport.session());

require('./config/passport')(passport);

app.use(cors());

app.use(bodyParser.json())//Получаем с помощью bodyParser все данные отправленные POST запросом(из формы получаем все файлы) в формате json

app.use(express.static(path.join(__dirname, 'public')));//Создаём статичную папку

mongoose.connect(config.db, { useNewUrlParser: true, useUnifiedTopology: true });

//Создаём слушателей которые уведомляют в консоле о успешном/не успешном подключение к бд
mongoose.connection.on('connected', () =>{
  console.log("Successfully connected to DB");
});
mongoose.connection.on('error);', (err) =>{
  console.log("Not successfully connected to DB: " + err);
});

app.get('/', (req, res) => {
  res.send('Home Page');
});

app.use('/account', account);
app.use('/leaderboard', leaderboard);


 app.get('*', (req, res) => {
   res.sendFile(path.join(__dirname,'public/index.html'));
 });

app.listen(port, () => {
  console.log("Server was started on port: " + port);
});

// var cron = require('node-cron');

// cron.schedule('*/2 * * * *', () => {
//   serv = codService.CodAPIService;
//   serv.
// });
